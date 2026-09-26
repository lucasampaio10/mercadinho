import { useCallback, useState } from 'react';
import {
  View, Text, StyleSheet, FlatList, TouchableOpacity,
  TextInput, Modal, ActivityIndicator, RefreshControl
} from 'react-native';
import { useFocusEffect, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { clientesApi, mensagemDeErro } from '../../src/api';
import type { Cliente } from '../../src/types';
import { colors, spacing, fontSize, radius } from '../../src/theme';

const formatBRL = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function FiadoScreen() {
  const router = useRouter();
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [modalNovoCliente, setModalNovoCliente] = useState(false);
  const [novoNome, setNovoNome] = useState('');
  const [novoTelefone, setNovoTelefone] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [erroLista, setErroLista] = useState<string | null>(null);
  const [erroNovoCliente, setErroNovoCliente] = useState<string | null>(null);

  const carregar = async () => {
    setErroLista(null);
    try {
      const data = await clientesApi.listarComFiado();
      setClientes(data);
    } catch (err) {
      setErroLista(mensagemDeErro(err, 'Erro ao carregar a lista de fiado.'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useFocusEffect(useCallback(() => { carregar(); }, []));

  const handleNovoCliente = async () => {
    if (!novoNome.trim()) return setErroNovoCliente('Informe o nome do cliente.');
    setSalvando(true);
    setErroNovoCliente(null);
    try {
      await clientesApi.criar({ nome: novoNome.trim(), telefone: novoTelefone.trim() || undefined });
      setNovoNome('');
      setNovoTelefone('');
      setModalNovoCliente(false);
      carregar();
    } catch (err) {
      // Mantém o modal aberto com o que foi digitado e mostra o motivo real.
      setErroNovoCliente(mensagemDeErro(err, 'Não foi possível salvar o cliente.'));
    } finally {
      setSalvando(false);
    }
  };

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.titulo}>Fiado</Text>
        <TouchableOpacity style={styles.btnNovo} onPress={() => setModalNovoCliente(true)}>
          <Ionicons name="person-add" size={18} color={colors.white} />
          <Text style={styles.btnNovoLabel}>Novo Cliente</Text>
        </TouchableOpacity>
      </View>

      {loading ? (
        <ActivityIndicator color={colors.primary} style={{ marginTop: 40 }} />
      ) : (
        <FlatList
          data={clientes}
          keyExtractor={c => c.id}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => { setRefreshing(true); carregar(); }} />}
          renderItem={({ item }) => (
            <TouchableOpacity
              style={styles.clienteCard}
              onPress={() => router.push(`/fiado/${item.id}` as any)}
            >
              <View style={styles.avatar}>
                <Text style={styles.avatarLetra}>{item.nome[0].toUpperCase()}</Text>
              </View>
              <View style={{ flex: 1 }}>
                <Text style={styles.clienteNome}>{item.nome}</Text>
                {item.telefone && (
                  <Text style={styles.clienteTelefone}>{item.telefone}</Text>
                )}
              </View>
              <View style={styles.saldoBox}>
                <Text style={styles.saldoLabel}>Deve</Text>
                <Text style={styles.saldoValor}>{formatBRL(item.saldoFiado)}</Text>
              </View>
              <Ionicons name="chevron-forward" size={20} color={colors.textMuted} />
            </TouchableOpacity>
          )}
          ListEmptyComponent={
            erroLista ? (
              <View style={styles.vazio}>
                <Ionicons name="cloud-offline-outline" size={48} color={colors.textMuted} />
                <Text style={styles.erroText}>{erroLista}</Text>
                <TouchableOpacity style={styles.btnTentarNovamente} onPress={() => { setLoading(true); carregar(); }}>
                  <Text style={styles.btnTentarNovamenteLabel}>Tentar novamente</Text>
                </TouchableOpacity>
              </View>
            ) : (
              <View style={styles.vazio}>
                <Ionicons name="checkmark-circle" size={48} color={colors.primary} />
                <Text style={styles.vazioText}>Nenhum fiado pendente!</Text>
              </View>
            )
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

            {erroNovoCliente && <Text style={styles.erroModal}>{erroNovoCliente}</Text>}

            <View style={styles.modalBotoes}>
              <TouchableOpacity
                style={[styles.btn, styles.btnCancelar]}
                onPress={() => {
                  setModalNovoCliente(false);
                  setNovoNome(''); setNovoTelefone(''); setErroNovoCliente(null);
                }}
              >
                <Text style={styles.btnCancelarLabel}>Cancelar</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[styles.btn, styles.btnSalvar, salvando && { opacity: 0.6 }]}
                onPress={handleNovoCliente}
                disabled={salvando}
              >
                {salvando
                  ? <ActivityIndicator color={colors.white} size="small" />
                  : <Text style={styles.btnSalvarLabel}>Salvar</Text>
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
  header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: spacing.lg },
  titulo: { fontSize: fontSize.xxl, fontWeight: '700', color: colors.text },
  btnNovo: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, backgroundColor: colors.primary, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  btnNovoLabel: { color: colors.white, fontWeight: '600', fontSize: fontSize.sm },
  clienteCard: { flexDirection: 'row', alignItems: 'center', backgroundColor: colors.surface, marginHorizontal: spacing.lg, marginBottom: spacing.sm, borderRadius: radius.lg, padding: spacing.md, gap: spacing.md, shadowColor: '#000', shadowOpacity: 0.05, shadowRadius: 4, elevation: 2 },
  avatar: { width: 44, height: 44, borderRadius: 22, backgroundColor: colors.primary + '20', alignItems: 'center', justifyContent: 'center' },
  avatarLetra: { fontSize: fontSize.lg, fontWeight: '700', color: colors.primary },
  clienteNome: { fontSize: fontSize.md, fontWeight: '600', color: colors.text },
  clienteTelefone: { fontSize: fontSize.xs, color: colors.textMuted, marginTop: 2 },
  saldoBox: { alignItems: 'flex-end', marginRight: spacing.sm },
  saldoLabel: { fontSize: fontSize.xs, color: colors.textMuted },
  saldoValor: { fontSize: fontSize.lg, fontWeight: '700', color: colors.danger },
  vazio: { alignItems: 'center', marginTop: 80, gap: spacing.md, paddingHorizontal: spacing.xl },
  vazioText: { fontSize: fontSize.lg, color: colors.textMuted },
  erroText: { fontSize: fontSize.md, color: colors.textMuted, textAlign: 'center' },
  btnTentarNovamente: { backgroundColor: colors.primary, borderRadius: radius.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
  btnTentarNovamenteLabel: { color: colors.white, fontWeight: '700' },
  erroModal: { color: colors.danger, fontSize: fontSize.sm, fontWeight: '600', textAlign: 'center', marginBottom: spacing.sm },
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
});
