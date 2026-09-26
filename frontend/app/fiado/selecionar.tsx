import { useCallback, useState } from 'react';
import {
  View, Text, StyleSheet, FlatList, TouchableOpacity,
  TextInput, ActivityIndicator, Modal
} from 'react-native';
import { useFocusEffect, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { clientesApi, mensagemDeErro } from '../../src/api';
import { useVendaStore } from '../../src/store/vendaStore';
import type { Cliente } from '../../src/types';
import { colors, spacing, fontSize, radius } from '../../src/theme';
import { useDialog } from '../../src/ui/dialog';

const formatBRL = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function SelecionarClienteScreen() {
  const router = useRouter();

  // Seletores em vez do store inteiro: sem eles, cada tecla na busca re-renderiza
  // a lista de clientes junto.
  const total = useVendaStore(s => s.total);
  const selecionarFormaPagamento = useVendaStore(s => s.selecionarFormaPagamento);
  const selecionarCliente = useVendaStore(s => s.selecionarCliente);
  const finalizarVenda = useVendaStore(s => s.finalizarVenda);
  const limparErro = useVendaStore(s => s.limparErro);

  const { confirmar, avisar } = useDialog();
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [loading, setLoading] = useState(true);
  const [busca, setBusca] = useState('');
  const [finalizando, setFinalizando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  // Cadastro de cliente sem sair da venda: descobrir aqui que o cliente ainda não
  // existe não deveria custar abandonar o carrinho e ir até a aba Fiado.
  const [modalNovoCliente, setModalNovoCliente] = useState(false);
  const [novoNome, setNovoNome] = useState('');
  const [novoTelefone, setNovoTelefone] = useState('');
  const [salvandoCliente, setSalvandoCliente] = useState(false);

  useFocusEffect(useCallback(() => {
    let ativo = true;
    setErro(null);
    clientesApi.listar()
      .then(lista => { if (ativo) setClientes(lista); })
      .catch(err => {
        if (ativo) setErro(mensagemDeErro(err, 'Erro ao carregar clientes.'));
      })
      .finally(() => { if (ativo) setLoading(false); });
    return () => { ativo = false; };
  }, []));

  const clientesFiltrados = clientes.filter(c =>
    c.nome.toLowerCase().includes(busca.toLowerCase())
  );

  /** Cadastra o cliente e já o deixa selecionado para o lançamento. */
  const handleNovoCliente = async () => {
    const nome = novoNome.trim();
    if (!nome) {
      await avisar({ titulo: 'Nome obrigatório', mensagem: 'Informe o nome do cliente.', tom: 'erro' });
      return;
    }

    setSalvandoCliente(true);
    try {
      const cliente = await clientesApi.criar({
        nome,
        telefone: novoTelefone.trim() || undefined,
      });
      setClientes(lista => [...lista, cliente].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')));
      setModalNovoCliente(false);
      setNovoNome('');
      setNovoTelefone('');
      setBusca('');
      setErro(null);
      await handleSelecionar(cliente);
    } catch (err) {
      const msg = mensagemDeErro(err, 'Erro ao cadastrar cliente.');
      setErro(msg);
      await avisar({ titulo: 'Erro ao cadastrar cliente', mensagem: msg, tom: 'erro' });
    } finally {
      setSalvandoCliente(false);
    }
  };

  const handleSelecionar = async (cliente: Cliente) => {
    const confirmou = await confirmar({
      titulo: 'Confirmar fiado',
      mensagem: `Lançar ${formatBRL(total)} no fiado de ${cliente.nome}?`,
    });
    if (!confirmou) return;

    selecionarFormaPagamento('Fiado');
    selecionarCliente(cliente);
    setFinalizando(true);
    setErro(null);
    const venda = await finalizarVenda();
    setFinalizando(false);

    if (!venda) {
      // O useEffect que exibe store.error vive em venda.tsx, não aqui —
      // sem isto a falha ficaria completamente silenciosa nesta tela.
      const msg = useVendaStore.getState().error ?? 'Não foi possível lançar o fiado.';
      setErro(msg);
      limparErro();
      await avisar({ titulo: 'Erro ao lançar fiado', mensagem: msg, tom: 'erro' });
      return;
    }

    // O valor confirmado é o que o backend lançou, não o total local.
    await avisar({
      titulo: 'Fiado lançado!',
      mensagem: `${formatBRL(venda.total)} adicionado ao fiado de ${cliente.nome}.`,
      tom: 'sucesso',
    });
    router.replace('/');
  };

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()}>
          <Ionicons name="arrow-back" size={24} color={colors.text} />
        </TouchableOpacity>
        <Text style={styles.titulo}>Selecionar Cliente</Text>
        <TouchableOpacity style={styles.btnNovo} onPress={() => setModalNovoCliente(true)}>
          <Ionicons name="person-add" size={16} color={colors.white} />
          <Text style={styles.btnNovoLabel}>Novo</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.totalBar}>
        <Text style={styles.totalLabel}>Total a lançar no fiado</Text>
        <Text style={styles.totalValor}>{formatBRL(total)}</Text>
      </View>

      {erro && (
        <View style={styles.erroBanner}>
          <Ionicons name="alert-circle" size={18} color={colors.danger} />
          <Text style={styles.erroTexto}>{erro}</Text>
        </View>
      )}

      <View style={styles.buscaContainer}>
        <Ionicons name="search" size={18} color={colors.textMuted} />
        <TextInput
          style={styles.buscaInput}
          placeholder="Buscar cliente..."
          value={busca}
          onChangeText={setBusca}
          autoFocus
        />
      </View>

      {loading || finalizando ? (
        <ActivityIndicator color={colors.primary} style={{ marginTop: 40 }} />
      ) : (
        <FlatList
          data={clientesFiltrados}
          keyExtractor={c => c.id}
          contentContainerStyle={{ padding: spacing.md }}
          renderItem={({ item }) => (
            <TouchableOpacity style={styles.clienteCard} onPress={() => handleSelecionar(item)}>
              <View style={styles.avatar}>
                <Text style={styles.avatarLetra}>{item.nome[0].toUpperCase()}</Text>
              </View>
              <View style={{ flex: 1 }}>
                <Text style={styles.clienteNome}>{item.nome}</Text>
                {item.saldoFiado > 0 && (
                  <Text style={styles.clienteSaldo}>Já deve {formatBRL(item.saldoFiado)}</Text>
                )}
              </View>
              <Ionicons name="chevron-forward" size={20} color={colors.textMuted} />
            </TouchableOpacity>
          )}
          ListEmptyComponent={
            <View style={styles.vazioBox}>
              <Text style={styles.vazio}>Nenhum cliente encontrado.</Text>
              <TouchableOpacity
                style={styles.btnCadastrar}
                onPress={() => { setNovoNome(busca.trim()); setModalNovoCliente(true); }}
              >
                <Ionicons name="person-add" size={18} color={colors.white} />
                <Text style={styles.btnCadastrarLabel}>
                  {busca.trim() ? `Cadastrar "${busca.trim()}"` : 'Cadastrar cliente'}
                </Text>
              </TouchableOpacity>
            </View>
          }
        />
      )}

      {/* Modal novo cliente */}
      <Modal visible={modalNovoCliente} transparent animationType="slide">
        <View style={styles.modalOverlay}>
          <View style={styles.modal}>
            <Text style={styles.modalTitulo}>Novo Cliente</Text>

            <Text style={styles.label}>Nome *</Text>
            <TextInput
              style={styles.input}
              value={novoNome}
              onChangeText={setNovoNome}
              placeholder="Nome do cliente"
              autoFocus
            />

            <Text style={styles.label}>Telefone (opcional)</Text>
            <TextInput
              style={styles.input}
              value={novoTelefone}
              onChangeText={setNovoTelefone}
              placeholder="(85) 99999-9999"
              keyboardType="phone-pad"
            />

            <View style={styles.modalBotoes}>
              <TouchableOpacity
                style={[styles.btn, styles.btnCancelar]}
                onPress={() => { setModalNovoCliente(false); setNovoNome(''); setNovoTelefone(''); }}
              >
                <Text style={styles.btnCancelarLabel}>Cancelar</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[styles.btn, styles.btnSalvar, salvandoCliente && { opacity: 0.6 }]}
                onPress={handleNovoCliente}
                disabled={salvandoCliente}
              >
                {salvandoCliente
                  ? <ActivityIndicator color={colors.white} size="small" />
                  : <Text style={styles.btnSalvarLabel}>Salvar e lançar</Text>
                }
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  header: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderBottomWidth: 1, borderColor: colors.border },
  titulo: { fontSize: fontSize.xl, fontWeight: '700', color: colors.text },
  totalBar: { backgroundColor: colors.warning + '20', padding: spacing.md, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', borderBottomWidth: 1, borderColor: colors.warning + '40' },
  totalLabel: { fontSize: fontSize.sm, color: colors.text, fontWeight: '600' },
  totalValor: { fontSize: fontSize.xl, fontWeight: '700', color: colors.warning },
  buscaContainer: { flexDirection: 'row', alignItems: 'center', margin: spacing.md, backgroundColor: colors.surface, borderRadius: radius.md, paddingHorizontal: spacing.md, borderWidth: 1, borderColor: colors.border, gap: spacing.sm },
  buscaInput: { flex: 1, paddingVertical: spacing.md, fontSize: fontSize.md, color: colors.text },
  clienteCard: { flexDirection: 'row', alignItems: 'center', backgroundColor: colors.surface, borderRadius: radius.lg, padding: spacing.md, marginBottom: spacing.sm, gap: spacing.md },
  avatar: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.primary + '20', alignItems: 'center', justifyContent: 'center' },
  avatarLetra: { fontSize: fontSize.lg, fontWeight: '700', color: colors.primary },
  clienteNome: { fontSize: fontSize.md, fontWeight: '600', color: colors.text },
  clienteSaldo: { fontSize: fontSize.xs, color: colors.danger, marginTop: 2 },
  vazioBox: { alignItems: 'center', marginTop: spacing.xl, gap: spacing.md },
  vazio: { textAlign: 'center', color: colors.textMuted },
  btnNovo: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs, marginLeft: 'auto', backgroundColor: colors.primary, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  btnNovoLabel: { color: colors.white, fontWeight: '700', fontSize: fontSize.sm },
  btnCadastrar: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, backgroundColor: colors.primary, borderRadius: radius.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  btnCadastrarLabel: { color: colors.white, fontWeight: '700', fontSize: fontSize.sm },
  modalOverlay: { flex: 1, backgroundColor: '#00000060', justifyContent: 'center', padding: spacing.xl },
  modal: { backgroundColor: colors.white, borderRadius: radius.xl, padding: spacing.xl },
  modalTitulo: { fontSize: fontSize.xl, fontWeight: '700', color: colors.text, marginBottom: spacing.lg },
  label: { fontSize: fontSize.sm, fontWeight: '600', color: colors.textMuted, marginBottom: spacing.xs },
  input: { borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, padding: spacing.md, fontSize: fontSize.md, color: colors.text, marginBottom: spacing.md },
  modalBotoes: { flexDirection: 'row', gap: spacing.md, marginTop: spacing.sm },
  btn: { flex: 1, borderRadius: radius.md, padding: spacing.md, alignItems: 'center' },
  btnCancelar: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border },
  btnCancelarLabel: { color: colors.textMuted, fontWeight: '600' },
  btnSalvar: { backgroundColor: colors.primary },
  btnSalvarLabel: { color: colors.white, fontWeight: '700' },
  erroBanner: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, backgroundColor: colors.danger + '15', borderLeftWidth: 3, borderLeftColor: colors.danger, paddingHorizontal: spacing.md, paddingVertical: spacing.sm, marginHorizontal: spacing.md, marginTop: spacing.md, borderRadius: radius.md },
  erroTexto: { flex: 1, color: colors.danger, fontSize: fontSize.sm, fontWeight: '600' },
});
