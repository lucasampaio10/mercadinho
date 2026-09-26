import { useCallback, useState } from 'react';
import {
  View, Text, StyleSheet, FlatList, TouchableOpacity,
  TextInput, Modal, ActivityIndicator
} from 'react-native';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { clientesApi, mensagemDeErro } from '../../src/api';
import type { Cliente, ItemFiado, Pagamento } from '../../src/types';
import { colors, spacing, fontSize, radius } from '../../src/theme';
import { useDialog } from '../../src/ui/dialog';

const formatBRL = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const formatData = (d: string) =>
  new Date(d).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });

/**
 * Extrato = compras e pagamentos na mesma linha do tempo.
 * Mostrar só as compras deixava o cliente sem ver o que já tinha quitado.
 */
type LinhaExtrato =
  | { tipo: 'compra'; id: string; data: string; compra: ItemFiado }
  | { tipo: 'pagamento'; id: string; data: string; pagamento: Pagamento };

function montarExtrato(compras: ItemFiado[], pagamentos: Pagamento[]): LinhaExtrato[] {
  return [
    ...compras.map(c => ({ tipo: 'compra' as const, id: `c-${c.id}`, data: c.createdAt, compra: c })),
    ...pagamentos.map(p => ({ tipo: 'pagamento' as const, id: `p-${p.id}`, data: p.createdAt, pagamento: p })),
  ].sort((a, b) => b.data.localeCompare(a.data));
}

export default function HistoricoFiadoScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { avisar } = useDialog();
  const [cliente, setCliente] = useState<Cliente | null>(null);
  const [historico, setHistorico] = useState<ItemFiado[]>([]);
  const [pagamentos, setPagamentos] = useState<Pagamento[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalPagamento, setModalPagamento] = useState(false);
  const [valorPagamento, setValorPagamento] = useState('');
  const [pagando, setPagando] = useState(false);
  // Falha ao carregar a tela — distinta de "cliente não existe".
  const [erroCarregar, setErroCarregar] = useState<string | null>(null);
  // Falha ao registrar o pagamento — exibida dentro do modal, que fica aberto.
  const [erroPagamento, setErroPagamento] = useState<string | null>(null);

  const carregar = async () => {
    setErroCarregar(null);
    try {
      const data = await clientesApi.historico(id);
      setCliente(data.cliente);
      setHistorico(data.historico);
      setPagamentos(data.pagamentos);
    } catch (err) {
      // Sem isto, uma queda de rede deixava `cliente` nulo e a tela dizia
      // "Cliente não encontrado." — mensagem errada para uma falha de conexão.
      setErroCarregar(mensagemDeErro(err, 'Erro ao carregar o histórico do cliente.'));
    } finally {
      setLoading(false);
    }
  };

  useFocusEffect(useCallback(() => { carregar(); }, [id]));

  const handlePagamento = async () => {
    const valor = parseFloat(valorPagamento.replace(',', '.'));
    if (isNaN(valor) || valor <= 0) return setErroPagamento('Informe um valor válido.');
    if (valor > (cliente?.saldoFiado ?? 0))
      return setErroPagamento('Valor excede o saldo devedor.');

    setPagando(true);
    setErroPagamento(null);
    try {
      await clientesApi.registrarPagamento(id, valor);
      setValorPagamento('');
      setModalPagamento(false);
      await carregar();
      await avisar({
        titulo: 'Pagamento registrado!',
        mensagem: `${formatBRL(valor)} abatido do fiado.`,
        tom: 'sucesso',
      });
    } catch (err) {
      // O modal continua aberto com o valor digitado: o operador vê o motivo
      // e pode corrigir, em vez de ficar sem saber se o pagamento entrou.
      setErroPagamento(mensagemDeErro(err, 'Não foi possível registrar o pagamento.'));
    } finally {
      setPagando(false);
    }
  };

  if (loading) return <ActivityIndicator color={colors.primary} style={{ flex: 1, marginTop: 60 }} />;

  if (erroCarregar) return (
    <View style={styles.center}>
      <Ionicons name="cloud-offline-outline" size={40} color={colors.textMuted} />
      <Text style={styles.erroText}>{erroCarregar}</Text>
      <TouchableOpacity
        style={styles.btnTentarNovamente}
        onPress={() => { setLoading(true); carregar(); }}
      >
        <Text style={styles.btnTentarNovamenteLabel}>Tentar novamente</Text>
      </TouchableOpacity>
    </View>
  );

  if (!cliente) return (
    <View style={styles.center}>
      <Text style={styles.erroText}>Cliente não encontrado.</Text>
    </View>
  );

  return (
    <View style={styles.container}>
      {/* Header cliente */}
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backBtn}>
          <Ionicons name="arrow-back" size={24} color={colors.text} />
        </TouchableOpacity>
        <View style={{ flex: 1 }}>
          <Text style={styles.clienteNome}>{cliente.nome}</Text>
          {cliente.telefone && <Text style={styles.clienteTelefone}>{cliente.telefone}</Text>}
        </View>
        <View style={styles.saldoBox}>
          <Text style={styles.saldoLabel}>Saldo devedor</Text>
          <Text style={styles.saldoValor}>{formatBRL(cliente.saldoFiado)}</Text>
        </View>
      </View>

      {/* Botão pagar */}
      {cliente.saldoFiado > 0 && (
        <TouchableOpacity style={styles.btnPagar} onPress={() => setModalPagamento(true)}>
          <Ionicons name="cash" size={20} color={colors.white} />
          <Text style={styles.btnPagarLabel}>Registrar Pagamento</Text>
        </TouchableOpacity>
      )}

      {/* Extrato — compras e pagamentos na mesma linha do tempo */}
      <FlatList
        data={montarExtrato(historico, pagamentos)}
        keyExtractor={linha => linha.id}
        contentContainerStyle={styles.lista}
        renderItem={({ item: linha }) => {
          if (linha.tipo === 'pagamento') {
            return (
              <View style={[styles.itemCard, styles.itemPagamento]}>
                <Ionicons name="cash-outline" size={18} color={colors.success} />
                <View style={{ flex: 1 }}>
                  <Text style={styles.itemData}>{formatData(linha.data)}</Text>
                  <Text style={styles.itemStatus}>Pagamento recebido</Text>
                </View>
                <Text style={styles.itemValorPagamento}>− {formatBRL(linha.pagamento.valor)}</Text>
              </View>
            );
          }

          const { compra } = linha;
          const parcial = !compra.pago && compra.valorPago > 0;
          return (
            <View style={[styles.itemCard, compra.pago && styles.itemPago]}>
              <View style={[styles.statusDot, {
                backgroundColor: compra.pago ? colors.success : parcial ? colors.primaryLight : colors.warning,
              }]} />
              <View style={{ flex: 1 }}>
                <Text style={styles.itemData}>{formatData(compra.createdAt)}</Text>
                <Text style={styles.itemStatus}>
                  {compra.pago
                    ? `Pago em ${formatData(compra.pagoEm!)}`
                    : parcial
                      ? `Parcial — abatido ${formatBRL(compra.valorPago)}, falta ${formatBRL(compra.saldo)}`
                      : 'Pendente'}
                </Text>
              </View>
              <Text style={[styles.itemValor, compra.pago && styles.itemValorPago]}>
                {formatBRL(compra.valor)}
              </Text>
            </View>
          );
        }}
        ListEmptyComponent={
          <Text style={styles.vazio}>Nenhuma compra no fiado.</Text>
        }
      />

      {/* Modal pagamento */}
      <Modal visible={modalPagamento} transparent animationType="slide">
        <View style={styles.modalOverlay}>
          <View style={styles.modal}>
            <Text style={styles.modalTitulo}>Registrar Pagamento</Text>
            <Text style={styles.modalSubtitulo}>
              Saldo devedor: <Text style={{ color: colors.danger, fontWeight: '700' }}>{formatBRL(cliente.saldoFiado)}</Text>
            </Text>

            <Text style={styles.label}>Valor recebido (R$)</Text>
            <TextInput
              style={styles.input}
              value={valorPagamento}
              onChangeText={setValorPagamento}
              keyboardType="decimal-pad"
              placeholder="0,00"
              autoFocus
            />
            <Text style={styles.dica}>Pode ser parcial ou o valor total.</Text>

            {erroPagamento && <Text style={styles.erroModal}>{erroPagamento}</Text>}

            <View style={styles.modalBotoes}>
              <TouchableOpacity
                style={[styles.btn, styles.btnCancelar]}
                onPress={() => { setModalPagamento(false); setValorPagamento(''); setErroPagamento(null); }}
              >
                <Text style={styles.btnCancelarLabel}>Cancelar</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[styles.btn, styles.btnConfirmar, pagando && { opacity: 0.6 }]}
                onPress={handlePagamento}
                disabled={pagando}
              >
                {pagando
                  ? <ActivityIndicator color={colors.white} size="small" />
                  : <Text style={styles.btnConfirmarLabel}>Confirmar</Text>
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
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.md, padding: spacing.xl },
  erroText: { color: colors.textMuted, fontSize: fontSize.lg, textAlign: 'center' },
  btnTentarNovamente: { backgroundColor: colors.primary, borderRadius: radius.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
  btnTentarNovamenteLabel: { color: colors.white, fontWeight: '700' },
  erroModal: { color: colors.danger, fontSize: fontSize.sm, fontWeight: '600', textAlign: 'center', marginBottom: spacing.md },
  header: { flexDirection: 'row', alignItems: 'center', padding: spacing.lg, backgroundColor: colors.surface, borderBottomWidth: 1, borderColor: colors.border, gap: spacing.md },
  backBtn: { padding: spacing.xs },
  clienteNome: { fontSize: fontSize.xl, fontWeight: '700', color: colors.text },
  clienteTelefone: { fontSize: fontSize.sm, color: colors.textMuted },
  saldoBox: { alignItems: 'flex-end' },
  saldoLabel: { fontSize: fontSize.xs, color: colors.textMuted },
  saldoValor: { fontSize: fontSize.xl, fontWeight: '700', color: colors.danger },
  btnPagar: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: colors.success, margin: spacing.lg, borderRadius: radius.lg, padding: spacing.md },
  btnPagarLabel: { color: colors.white, fontSize: fontSize.md, fontWeight: '700' },
  lista: { padding: spacing.lg, gap: spacing.sm },
  itemCard: { flexDirection: 'row', alignItems: 'center', backgroundColor: colors.surface, borderRadius: radius.lg, padding: spacing.md, gap: spacing.md, borderLeftWidth: 4, borderLeftColor: colors.warning },
  itemPago: { opacity: 0.6, borderLeftColor: colors.success },
  itemPagamento: { borderLeftColor: colors.success, backgroundColor: colors.success + '12' },
  itemValorPagamento: { fontSize: fontSize.lg, fontWeight: '700', color: colors.success },
  statusDot: { width: 10, height: 10, borderRadius: 5 },
  itemData: { fontSize: fontSize.sm, fontWeight: '600', color: colors.text },
  itemStatus: { fontSize: fontSize.xs, color: colors.textMuted, marginTop: 2 },
  itemValor: { fontSize: fontSize.lg, fontWeight: '700', color: colors.danger },
  itemValorPago: { color: colors.success },
  vazio: { textAlign: 'center', color: colors.textMuted, marginTop: spacing.xl },
  modalOverlay: { flex: 1, backgroundColor: '#00000060', justifyContent: 'center', padding: spacing.xl },
  modal: { backgroundColor: colors.white, borderRadius: radius.xl, padding: spacing.xl },
  modalTitulo: { fontSize: fontSize.xl, fontWeight: '700', color: colors.text, marginBottom: spacing.xs },
  modalSubtitulo: { fontSize: fontSize.md, color: colors.textMuted, marginBottom: spacing.lg },
  label: { fontSize: fontSize.sm, fontWeight: '600', color: colors.textMuted, marginBottom: spacing.xs },
  input: { borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, padding: spacing.md, fontSize: fontSize.xxl, fontWeight: '700', color: colors.text, marginBottom: spacing.xs, textAlign: 'center' },
  dica: { fontSize: fontSize.xs, color: colors.textMuted, textAlign: 'center', marginBottom: spacing.lg },
  modalBotoes: { flexDirection: 'row', gap: spacing.md },
  btn: { flex: 1, borderRadius: radius.md, padding: spacing.md, alignItems: 'center' },
  btnCancelar: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border },
  btnCancelarLabel: { color: colors.textMuted, fontWeight: '600' },
  btnConfirmar: { backgroundColor: colors.success },
  btnConfirmarLabel: { color: colors.white, fontWeight: '700' },
});
