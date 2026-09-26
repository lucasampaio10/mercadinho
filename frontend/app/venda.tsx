import { useCallback, useEffect, useRef, useState } from 'react';
import {
  View, Text, StyleSheet, TextInput, FlatList,
  TouchableOpacity, ActivityIndicator, ScrollView, Modal
} from 'react-native';
import { useFocusEffect, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { CameraView, useCameraPermissions, type BarcodeScanningResult } from 'expo-camera';
import { mensagemDeErro, produtosApi } from '../src/api';
import { useVendaStore } from '../src/store/vendaStore';
import type { FormaPagamento, Produto } from '../src/types';
import { colors, spacing, fontSize, radius } from '../src/theme';
import { useDialog } from '../src/ui/dialog';

const formatBRL = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function VendaScreen() {
  const router = useRouter();

  // Seletores em vez de `useVendaStore()` inteiro: sem eles, a tela toda — incluindo
  // a FlatList de resultados — re-renderiza a cada tecla digitada na busca.
  const itens = useVendaStore(s => s.itens);
  const total = useVendaStore(s => s.total);
  const error = useVendaStore(s => s.error);
  const hidratado = useVendaStore(s => s.hidratado);
  const adicionarItem = useVendaStore(s => s.adicionarItem);
  const alterarQuantidade = useVendaStore(s => s.alterarQuantidade);
  const removerItem = useVendaStore(s => s.removerItem);
  const cancelarVenda = useVendaStore(s => s.cancelarVenda);
  const selecionarFormaPagamento = useVendaStore(s => s.selecionarFormaPagamento);
  const observacao = useVendaStore(s => s.observacao);
  const definirObservacao = useVendaStore(s => s.definirObservacao);
  const finalizarVenda = useVendaStore(s => s.finalizarVenda);
  const limparErro = useVendaStore(s => s.limparErro);

  const { confirmar, avisar } = useDialog();
  const [busca, setBusca] = useState('');
  const [produtos, setProdutos] = useState<Produto[]>([]);
  const [loadingBusca, setLoadingBusca] = useState(false);
  const [erroBusca, setErroBusca] = useState<string | null>(null);
  const [finalizando, setFinalizando] = useState(false);
  const [scannerAberto, setScannerAberto] = useState(false);
  const [buscandoCodigo, setBuscandoCodigo] = useState(false);
  const [permissao, solicitarPermissao] = useCameraPermissions();
  const jaLeu = useRef(false);

  // Reconcilia o carrinho com o servidor sempre que a tela ganha foco — é o que
  // devolve a venda em andamento depois de um F5. Só roda após a hidratação do
  // storage, que é de onde o vendaId vem.
  // Lê o estado na hora em vez de fechar sobre `vendaId`: se `vendaId` estivesse nas
  // dependências, abrir a venda ao adicionar o 1º item redispararia a restauração e o
  // GET poderia sobrescrever o carrinho com o estado anterior ao POST do item.
  useFocusEffect(useCallback(() => {
    const { hidratado: pronto, vendaId, restaurarVenda } = useVendaStore.getState();
    if (pronto && vendaId) restaurarVenda();
  }, [hidratado]));

  useEffect(() => {
    if (!error) return;
    // Limpa antes de abrir: se o erro continuasse no estado, o diálogo reabriria
    // a cada re-render.
    limparErro();
    avisar({ titulo: 'Erro', mensagem: error, tom: 'erro' });
  }, [error]);

  useEffect(() => {
    if (busca.length < 2) { setProdutos([]); setErroBusca(null); return; }

    // O debounce cancela o timer, mas não a requisição já em voo. Sem abortar,
    // limpar a busca e receber depois a resposta antiga repovoa a lista com
    // resultados que não correspondem mais ao que está digitado.
    const controller = new AbortController();
    const t = setTimeout(async () => {
      setLoadingBusca(true);
      setErroBusca(null);
      try {
        setProdutos(await produtosApi.buscar(busca, controller.signal));
      } catch (err) {
        if (controller.signal.aborted) return; // substituída por uma busca mais nova
        setProdutos([]);
        setErroBusca(mensagemDeErro(err, 'Erro ao buscar produtos.'));
      } finally {
        if (!controller.signal.aborted) setLoadingBusca(false);
      }
    }, 300);

    return () => { clearTimeout(t); controller.abort(); };
  }, [busca]);

  const temItens = itens.length > 0;

  const handleFinalizar = async (forma: FormaPagamento) => {
    if (!temItens) return;
    if (forma === 'Fiado') {
      router.push('/fiado/selecionar');
      return;
    }

    const confirmou = await confirmar({
      titulo: 'Confirmar venda',
      mensagem: `Finalizar com ${forma}?\nTotal: ${formatBRL(total)}`,
    });
    if (!confirmou) return;

    selecionarFormaPagamento(forma);
    setFinalizando(true);
    const venda = await finalizarVenda();
    setFinalizando(false);

    // Em caso de falha o erro já está em store.error e o useEffect acima o exibe.
    if (!venda) return;

    // Confirma com o total gravado no servidor, não com o que estava na tela.
    await avisar({
      titulo: 'Venda finalizada!',
      mensagem: `Total: ${formatBRL(venda.total)}`,
      tom: 'sucesso',
    });
    router.replace('/');
  };

  const abrirScanner = async () => {
    if (!permissao?.granted) {
      const resultado = await solicitarPermissao();
      if (!resultado.granted) {
        return avisar({
          titulo: 'Permissão da câmera negada',
          mensagem: 'Ative a câmera para o PDV nas configurações do celular para escanear o código de barras.',
        });
      }
    }
    jaLeu.current = false;
    setScannerAberto(true);
  };

  const aoLerCodigo = async ({ data }: BarcodeScanningResult) => {
    if (jaLeu.current) return;
    jaLeu.current = true;
    setScannerAberto(false);
    setBuscandoCodigo(true);
    try {
      const produto = await produtosApi.buscarPorCodigoBarras(data);
      if (!produto) {
        await avisar({ titulo: 'Produto não encontrado', mensagem: `Nenhum produto com o código "${data}".`, tom: 'erro' });
        return;
      }
      await adicionarItem(produto);
    } catch (err) {
      await avisar({ titulo: 'Erro ao buscar produto', mensagem: mensagemDeErro(err, 'Erro desconhecido.'), tom: 'erro' });
    } finally {
      setBuscandoCodigo(false);
    }
  };

  const handleCancelarVenda = async () => {
    const confirmou = await confirmar({
      titulo: 'Cancelar a venda?',
      mensagem: 'O carrinho será descartado.',
      confirmarLabel: 'Cancelar venda',
      cancelarLabel: 'Voltar',
      tom: 'perigo',
    });
    if (confirmou) await cancelarVenda();
  };

  return (
    <View style={styles.container}>

      {/* Busca */}
      <View style={styles.buscaContainer}>
        <Ionicons name="search" size={18} color={colors.textMuted} />
        <TextInput
          style={styles.buscaInput}
          placeholder="Buscar produto..."
          value={busca}
          onChangeText={setBusca}
          autoCorrect={false}
        />
        {loadingBusca
          ? <ActivityIndicator size="small" color={colors.primary} />
          : busca.length > 0
            ? <TouchableOpacity onPress={() => { setBusca(''); setProdutos([]); }}>
                <Ionicons name="close-circle" size={18} color={colors.textMuted} />
              </TouchableOpacity>
            : null
        }
        <TouchableOpacity onPress={abrirScanner} disabled={buscandoCodigo} accessibilityLabel="Escanear código de barras">
          {buscandoCodigo
            ? <ActivityIndicator size="small" color={colors.primary} />
            : <Ionicons name="barcode-outline" size={22} color={colors.primary} />
          }
        </TouchableOpacity>
      </View>

      {/* Lista de resultados */}
      <FlatList
        style={styles.lista}
        data={produtos}
        keyExtractor={p => p.id}
        contentContainerStyle={{ padding: spacing.md, gap: spacing.sm }}
        renderItem={({ item }) => (
          <TouchableOpacity style={styles.produtoItem} onPress={() => adicionarItem(item)}>
            <View style={{ flex: 1 }}>
              <Text style={styles.produtoNome}>{item.nome}</Text>
              {item.categoria && <Text style={styles.produtoCategoria}>{item.categoria}</Text>}
            </View>
            <Text style={styles.produtoPreco}>{formatBRL(item.preco)}</Text>
            <View style={styles.btnAdd}>
              <Ionicons name="add" size={20} color={colors.white} />
            </View>
          </TouchableOpacity>
        )}
        ListEmptyComponent={
          erroBusca
            ? <Text style={styles.erro}>{erroBusca}</Text>
            : busca.length >= 2 && !loadingBusca
              ? <Text style={styles.vazio}>Nenhum produto encontrado.</Text>
              : busca.length === 0
                ? <Text style={styles.dica}>Digite o nome do produto para buscar</Text>
                : null
        }
      />

      {/* Painel inferior — carrinho + pagamento */}
      <View style={styles.painel}>

        {/* Itens do carrinho */}
        {temItens && (
          <ScrollView style={styles.carrinhoScroll} contentContainerStyle={{ gap: 4 }}>
            {itens.map(item => (
              <View key={item.produtoId} style={styles.carrinhoItem}>
                <Text style={styles.itemNome} numberOfLines={1}>{item.nomeProduto}</Text>

                {/* Ajuste direto da quantidade: corrigir um "10" digitado no lugar de
                    "1" não deve exigir apagar o item e buscá-lo de novo. */}
                <View style={styles.stepper}>
                  <TouchableOpacity
                    style={styles.stepperBtn}
                    onPress={() => alterarQuantidade(item.produtoId, item.quantidade - 1)}
                    accessibilityLabel={`Diminuir ${item.nomeProduto}`}
                  >
                    <Ionicons name="remove" size={14} color={colors.primary} />
                  </TouchableOpacity>
                  <Text style={styles.stepperQtd}>{item.quantidade}</Text>
                  <TouchableOpacity
                    style={styles.stepperBtn}
                    onPress={() => alterarQuantidade(item.produtoId, item.quantidade + 1)}
                    accessibilityLabel={`Aumentar ${item.nomeProduto}`}
                  >
                    <Ionicons name="add" size={14} color={colors.primary} />
                  </TouchableOpacity>
                </View>

                <Text style={styles.itemSubtotal}>{formatBRL(item.subtotal)}</Text>
                <TouchableOpacity
                  onPress={() => removerItem(item.produtoId)}
                  accessibilityLabel={`Remover ${item.nomeProduto}`}
                >
                  <Ionicons name="trash-outline" size={16} color={colors.danger} />
                </TouchableOpacity>
              </View>
            ))}
          </ScrollView>
        )}

        {/* Observação — vale para as 3 formas de pagamento, só entra na venda ao finalizar. */}
        {temItens && (
          <TextInput
            style={styles.observacaoInput}
            placeholder="Observação (opcional) — ex: sem troco, entregar depois"
            placeholderTextColor={colors.textMuted}
            value={observacao}
            onChangeText={definirObservacao}
          />
        )}

        {/* Total */}
        <View style={styles.totalRow}>
          <Text style={styles.totalLabel}>
            {temItens ? `${itens.reduce((a, i) => a + i.quantidade, 0)} itens` : 'Carrinho vazio'}
          </Text>
          {temItens && (
            <TouchableOpacity
              style={styles.btnCancelarVenda}
              onPress={handleCancelarVenda}
              disabled={finalizando}
            >
              <Ionicons name="close" size={14} color={colors.danger} />
              <Text style={styles.btnCancelarVendaLabel}>Cancelar venda</Text>
            </TouchableOpacity>
          )}
          <Text style={[styles.totalValor, !temItens && { color: colors.textMuted }]}>
            {formatBRL(total)}
          </Text>
        </View>

        {/* Botões de pagamento */}
        <View style={styles.pagamentos}>
          {(['Dinheiro', 'Cartao', 'Fiado'] as FormaPagamento[]).map(forma => (
            <TouchableOpacity
              key={forma}
              style={[
                styles.btnPagamento,
                forma === 'Fiado' && styles.btnFiado,
                (!temItens || finalizando) && styles.btnDesabilitado,
              ]}
              onPress={() => handleFinalizar(forma)}
              disabled={!temItens || finalizando}
            >
              {finalizando
                ? <ActivityIndicator size="small" color={colors.white} />
                : <>
                    <Ionicons
                      name={forma === 'Dinheiro' ? 'cash' : forma === 'Cartao' ? 'card' : 'people'}
                      size={18}
                      color={!temItens ? colors.textMuted : forma === 'Fiado' ? colors.white : colors.primary}
                    />
                    <Text style={[
                      styles.btnLabel,
                      forma === 'Fiado' && temItens && { color: colors.white },
                      !temItens && { color: colors.textMuted },
                    ]}>
                      {forma}
                    </Text>
                  </>
              }
            </TouchableOpacity>
          ))}
        </View>
      </View>

      {/* Scanner de código de barras */}
      <Modal visible={scannerAberto} animationType="slide">
        <View style={styles.scannerContainer}>
          <CameraView
            style={StyleSheet.absoluteFill}
            facing="back"
            barcodeScannerSettings={{
              barcodeTypes: ['ean13', 'ean8', 'upc_a', 'upc_e', 'code128', 'code39', 'qr'],
            }}
            onBarcodeScanned={aoLerCodigo}
          />
          <View style={styles.scannerOverlay}>
            <Text style={styles.scannerDica}>Aponte a câmera para o código de barras do produto</Text>
            <TouchableOpacity style={styles.btnFecharScanner} onPress={() => setScannerAberto(false)}>
              <Ionicons name="close" size={24} color={colors.white} />
              <Text style={styles.btnFecharScannerLabel}>Cancelar</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>

    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  buscaContainer: {
    flexDirection: 'row', alignItems: 'center',
    backgroundColor: colors.surface, margin: spacing.md,
    borderRadius: radius.md, paddingHorizontal: spacing.md,
    borderWidth: 1, borderColor: colors.border, gap: spacing.sm,
  },
  buscaInput: { flex: 1, paddingVertical: spacing.md, fontSize: fontSize.md, color: colors.text },
  lista: { flex: 1 },
  produtoItem: {
    flexDirection: 'row', alignItems: 'center',
    backgroundColor: colors.surface, borderRadius: radius.lg,
    padding: spacing.md, borderWidth: 1, borderColor: colors.border, gap: spacing.sm,
  },
  produtoNome: { fontSize: fontSize.md, fontWeight: '700', color: colors.text },
  produtoCategoria: { fontSize: fontSize.xs, color: colors.textMuted },
  produtoPreco: { fontSize: fontSize.md, fontWeight: '700', color: colors.primary },
  btnAdd: { backgroundColor: colors.primary, borderRadius: radius.md, padding: 6 },
  vazio: { textAlign: 'center', color: colors.textMuted, marginTop: spacing.xl },
  erro: { textAlign: 'center', color: colors.danger, marginTop: spacing.xl, paddingHorizontal: spacing.lg },
  dica: { textAlign: 'center', color: colors.textMuted, marginTop: 60, fontSize: fontSize.md },
  painel: {
    backgroundColor: colors.surface,
    borderTopWidth: 1, borderColor: colors.border,
    padding: spacing.md, gap: spacing.sm,
  },
  carrinhoScroll: { maxHeight: 100 },
  carrinhoItem: {
    flexDirection: 'row', alignItems: 'center',
    gap: spacing.sm, paddingVertical: 2,
  },
  itemNome: { flex: 1, fontSize: fontSize.sm, color: colors.text },
  stepper: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.xs,
    borderWidth: 1, borderColor: colors.border, borderRadius: radius.sm,
    paddingHorizontal: 2,
  },
  stepperBtn: { padding: 4 },
  stepperQtd: {
    minWidth: 22, textAlign: 'center',
    fontSize: fontSize.sm, fontWeight: '700', color: colors.text,
  },
  itemSubtotal: { minWidth: 72, textAlign: 'right', fontSize: fontSize.sm, fontWeight: '700', color: colors.text },
  totalRow: {
    flexDirection: 'row', justifyContent: 'space-between',
    alignItems: 'center', paddingTop: spacing.sm,
    borderTopWidth: 1, borderColor: colors.border,
  },
  totalLabel: { fontSize: fontSize.sm, color: colors.textMuted },
  btnCancelarVenda: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.xs,
    marginLeft: spacing.md, marginRight: 'auto',
    paddingVertical: spacing.xs, paddingHorizontal: spacing.sm,
    borderRadius: radius.sm, borderWidth: 1, borderColor: colors.danger + '60',
  },
  btnCancelarVendaLabel: { fontSize: fontSize.xs, color: colors.danger, fontWeight: '600' },
  totalValor: { fontSize: fontSize.xl, fontWeight: '700', color: colors.primary },
  pagamentos: { flexDirection: 'row', gap: spacing.sm },
  btnPagamento: {
    flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
    gap: spacing.xs, borderRadius: radius.md, paddingVertical: spacing.md,
    borderWidth: 2, borderColor: colors.primary, backgroundColor: colors.white,
  },
  btnFiado: { backgroundColor: colors.warning, borderColor: colors.warning },
  btnDesabilitado: { borderColor: colors.border, backgroundColor: colors.surface },
  btnLabel: { fontSize: fontSize.sm, fontWeight: '700', color: colors.primary },
  observacaoInput: {
    borderWidth: 1, borderColor: colors.border, borderRadius: radius.md,
    paddingHorizontal: spacing.md, paddingVertical: spacing.sm,
    fontSize: fontSize.sm, color: colors.text,
  },
  scannerContainer: { flex: 1, backgroundColor: '#000' },
  scannerOverlay: {
    position: 'absolute', bottom: 0, left: 0, right: 0,
    padding: spacing.xl, alignItems: 'center', gap: spacing.lg,
    backgroundColor: '#00000080',
  },
  scannerDica: { color: colors.white, fontSize: fontSize.md, fontWeight: '600', textAlign: 'center' },
  btnFecharScanner: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.sm,
    backgroundColor: colors.danger, borderRadius: radius.md,
    paddingHorizontal: spacing.lg, paddingVertical: spacing.sm,
  },
  btnFecharScannerLabel: { color: colors.white, fontWeight: '700', fontSize: fontSize.sm },
});
