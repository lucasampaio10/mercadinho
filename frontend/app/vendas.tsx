import { useCallback, useState } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, ActivityIndicator, Modal, ScrollView } from 'react-native';
import { useFocusEffect, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { mensagemDeErro, vendasApi } from '../src/api';
import type { FormaPagamento, Venda } from '../src/types';
import { colors, spacing, fontSize, radius } from '../src/theme';

const formatBRL = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

// `new Date(iso)` interpreta o "Z" do backend como instante UTC e formata no fuso do
// próprio aparelho — é assim que a hora exibida acompanha o dispositivo do operador,
// não o servidor onde a API roda.
const formatHora = (d: string) => new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
const formatDataHoraCompleta = (d: string) =>
  new Date(d).toLocaleString('pt-BR', { day: '2-digit', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });

const ICONE_FORMA: Record<FormaPagamento, keyof typeof Ionicons.glyphMap> = {
  Dinheiro: 'cash',
  Cartao: 'card',
  Fiado: 'people',
};

export default function VendasScreen() {
  const router = useRouter();
  const [vendas, setVendas] = useState<Venda[]>([]);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [selecionada, setSelecionada] = useState<Venda | null>(null);

  const carregar = useCallback(async () => {
    setErro(null);
    try {
      setVendas(await vendasApi.listarHoje());
    } catch (err) {
      setErro(mensagemDeErro(err, 'Erro ao carregar as vendas.'));
    } finally {
      setLoading(false);
    }
  }, []);

  // Recarrega ao voltar pra tela — uma venda pode ter sido feita desde a última vez.
  useFocusEffect(useCallback(() => { carregar(); }, [carregar]));

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backBtn}>
          <Ionicons name="arrow-back" size={24} color={colors.text} />
        </TouchableOpacity>
        <Text style={styles.titulo}>Vendas de Hoje</Text>
      </View>

      {loading ? (
        <ActivityIndicator color={colors.primary} style={{ flex: 1 }} />
      ) : erro ? (
        <View style={styles.center}>
          <Ionicons name="cloud-offline-outline" size={40} color={colors.textMuted} />
          <Text style={styles.erroText}>{erro}</Text>
          <TouchableOpacity style={styles.btnTentarNovamente} onPress={() => { setLoading(true); carregar(); }}>
            <Text style={styles.btnTentarNovamenteLabel}>Tentar novamente</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <FlatList
          data={vendas}
          keyExtractor={v => v.id}
          contentContainerStyle={styles.lista}
          renderItem={({ item }) => (
            <TouchableOpacity style={styles.card} onPress={() => setSelecionada(item)}>
              <View style={[styles.iconeForma, item.formaPagamento === 'Fiado' && styles.iconeFiado]}>
                <Ionicons
                  name={item.formaPagamento ? ICONE_FORMA[item.formaPagamento] : 'receipt-outline'}
                  size={18}
                  color={item.formaPagamento === 'Fiado' ? colors.white : colors.primary}
                />
              </View>
              <View style={{ flex: 1 }}>
                <Text style={styles.cardHora}>
                  {formatHora(item.createdAt)} · {item.formaPagamento ?? '—'}
                  {item.clienteNome ? ` · ${item.clienteNome}` : ''}
                </Text>
                <Text style={styles.cardItens} numberOfLines={1}>
                  {item.itens.map(i => `${i.quantidade}x ${i.nomeProduto}`).join(', ')}
                </Text>
              </View>
              <Text style={styles.cardTotal}>{formatBRL(item.total)}</Text>
              <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
            </TouchableOpacity>
          )}
          ListEmptyComponent={<Text style={styles.vazio}>Nenhuma venda finalizada hoje ainda.</Text>}
        />
      )}

      {/* Detalhe da venda */}
      <Modal visible={selecionada !== null} transparent animationType="slide" onRequestClose={() => setSelecionada(null)}>
        <View style={styles.modalOverlay}>
          <View style={styles.modal}>
            {selecionada && (
              <>
                <View style={styles.modalHeader}>
                  <View style={[styles.iconeForma, selecionada.formaPagamento === 'Fiado' && styles.iconeFiado]}>
                    <Ionicons
                      name={selecionada.formaPagamento ? ICONE_FORMA[selecionada.formaPagamento] : 'receipt-outline'}
                      size={20}
                      color={selecionada.formaPagamento === 'Fiado' ? colors.white : colors.primary}
                    />
                  </View>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.modalForma}>{selecionada.formaPagamento ?? '—'}</Text>
                    <Text style={styles.modalData}>{formatDataHoraCompleta(selecionada.createdAt)}</Text>
                  </View>
                  <TouchableOpacity onPress={() => setSelecionada(null)}>
                    <Ionicons name="close" size={24} color={colors.textMuted} />
                  </TouchableOpacity>
                </View>

                {selecionada.clienteNome && (
                  <View style={styles.linhaInfo}>
                    <Ionicons name="person-outline" size={16} color={colors.textMuted} />
                    <Text style={styles.linhaInfoTexto}>{selecionada.clienteNome}</Text>
                  </View>
                )}

                <ScrollView style={styles.itensScroll}>
                  {selecionada.itens.map(item => (
                    <View key={item.produtoId} style={styles.itemLinha}>
                      <View style={{ flex: 1 }}>
                        <Text style={styles.itemNome}>{item.nomeProduto}</Text>
                        <Text style={styles.itemDetalhe}>
                          {item.quantidade}x {formatBRL(item.precoUnitario)}
                        </Text>
                      </View>
                      <Text style={styles.itemSubtotal}>{formatBRL(item.subtotal)}</Text>
                    </View>
                  ))}
                </ScrollView>

                <View style={styles.totalRow}>
                  <Text style={styles.totalLabel}>Total</Text>
                  <Text style={styles.totalValor}>{formatBRL(selecionada.total)}</Text>
                </View>

                {selecionada.observacao && (
                  <View style={styles.observacaoBox}>
                    <Ionicons name="chatbox-ellipses-outline" size={16} color={colors.textMuted} />
                    <Text style={styles.observacaoTexto}>{selecionada.observacao}</Text>
                  </View>
                )}
              </>
            )}
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  header: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.md,
    padding: spacing.lg, backgroundColor: colors.surface,
    borderBottomWidth: 1, borderColor: colors.border,
  },
  backBtn: { padding: spacing.xs },
  titulo: { fontSize: fontSize.xl, fontWeight: '700', color: colors.text },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.md, padding: spacing.xl },
  erroText: { color: colors.textMuted, fontSize: fontSize.lg, textAlign: 'center' },
  btnTentarNovamente: { backgroundColor: colors.primary, borderRadius: radius.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
  btnTentarNovamenteLabel: { color: colors.white, fontWeight: '700' },
  lista: { padding: spacing.lg, gap: spacing.sm },
  card: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.md,
    backgroundColor: colors.surface, borderRadius: radius.lg,
    padding: spacing.md, borderWidth: 1, borderColor: colors.border,
  },
  iconeForma: { width: 36, height: 36, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.primary + '20' },
  iconeFiado: { backgroundColor: colors.warning },
  cardHora: { fontSize: fontSize.xs, color: colors.textMuted, marginBottom: 2 },
  cardItens: { fontSize: fontSize.sm, color: colors.text },
  cardTotal: { fontSize: fontSize.md, fontWeight: '700', color: colors.primary },
  vazio: { textAlign: 'center', color: colors.textMuted, marginTop: spacing.xl },
  modalOverlay: { flex: 1, backgroundColor: '#00000060', justifyContent: 'flex-end' },
  modal: { backgroundColor: colors.surface, borderTopLeftRadius: radius.xl, borderTopRightRadius: radius.xl, padding: spacing.lg, maxHeight: '80%' },
  modalHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, marginBottom: spacing.md },
  modalForma: { fontSize: fontSize.lg, fontWeight: '700', color: colors.text },
  modalData: { fontSize: fontSize.xs, color: colors.textMuted, marginTop: 2, textTransform: 'capitalize' },
  linhaInfo: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.md },
  linhaInfoTexto: { fontSize: fontSize.sm, color: colors.text, fontWeight: '600' },
  itensScroll: { maxHeight: 260, marginBottom: spacing.md },
  itemLinha: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.md,
    paddingVertical: spacing.sm, borderBottomWidth: 1, borderColor: colors.border,
  },
  itemNome: { fontSize: fontSize.sm, fontWeight: '600', color: colors.text },
  itemDetalhe: { fontSize: fontSize.xs, color: colors.textMuted, marginTop: 2 },
  itemSubtotal: { fontSize: fontSize.sm, fontWeight: '700', color: colors.text },
  totalRow: {
    flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center',
    paddingTop: spacing.md, borderTopWidth: 1, borderColor: colors.border,
  },
  totalLabel: { fontSize: fontSize.md, color: colors.textMuted, fontWeight: '600' },
  totalValor: { fontSize: fontSize.xl, fontWeight: '700', color: colors.primary },
  observacaoBox: {
    flexDirection: 'row', gap: spacing.sm, backgroundColor: colors.background,
    borderRadius: radius.md, padding: spacing.md, marginTop: spacing.md,
  },
  observacaoTexto: { flex: 1, fontSize: fontSize.sm, color: colors.text, fontStyle: 'italic' },
});
