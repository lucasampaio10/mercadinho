import { useCallback, useEffect, useRef, useState } from 'react';
import {
  View, Text, StyleSheet, FlatList, TextInput,
  TouchableOpacity, Modal, ActivityIndicator
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { CameraView, useCameraPermissions, type BarcodeScanningResult } from 'expo-camera';
import { mensagemDeErro, produtosApi } from '../src/api';
import type { Produto } from '../src/types';
import { colors, spacing, fontSize, radius } from '../src/theme';
import { useDialog } from '../src/ui/dialog';

const formatBRL = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

const FORM_VAZIO = { nome: '', preco: '', estoque: '', categoria: '', codigoBarras: '' };

// Aceita "0,50" (decimal-pad em pt-BR) e "0.50"
const parsePreco = (v: string) => parseFloat(v.trim().replace(',', '.'));

/**
 * Estoque vazio conta como 0; qualquer outra coisa precisa ser um inteiro não negativo.
 * `parseInt` sozinho devolve NaN para "abc", que vira `null` no JSON e faz o model
 * binder responder 400 com uma mensagem crua de serialização — em vez disso o campo é
 * validado aqui, do mesmo jeito que o preço já era.
 */
const parseEstoque = (v: string): number | null => {
  const texto = v.trim();
  if (!texto) return 0;
  if (!/^\d+$/.test(texto)) return null;

  const n = Number(texto);
  return Number.isSafeInteger(n) ? n : null;
};

export default function ProdutosScreen() {
  const { confirmar, avisar } = useDialog();
  const [produtos, setProdutos] = useState<Produto[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalAberto, setModalAberto] = useState(false);
  const [editando, setEditando] = useState<Produto | null>(null);
  const [form, setForm] = useState(FORM_VAZIO);
  const [salvando, setSalvando] = useState(false);
  const [scannerAberto, setScannerAberto] = useState(false);
  const [permissao, solicitarPermissao] = useCameraPermissions();
  const jaLeu = useRef(false);

  const carregar = useCallback(async () => {
    setLoading(true);
    try { setProdutos(await produtosApi.listar()); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { carregar(); }, [carregar]);

  const abrirModal = (produto?: Produto) => {
    setEditando(produto ?? null);
    setForm(produto
      ? {
          nome: produto.nome,
          preco: String(produto.preco).replace('.', ','),
          estoque: String(produto.estoque),
          categoria: produto.categoria ?? '',
          codigoBarras: produto.codigoBarras ?? '',
        }
      : FORM_VAZIO
    );
    setModalAberto(true);
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

  const aoLerCodigo = ({ data }: BarcodeScanningResult) => {
    if (jaLeu.current) return;
    jaLeu.current = true;
    setForm(f => ({ ...f, codigoBarras: data }));
    setScannerAberto(false);
  };

  const salvar = async () => {
    if (!form.nome || !form.preco) {
      return avisar({ titulo: 'Preencha nome e preço.' });
    }
    const preco = parsePreco(form.preco);
    if (!Number.isFinite(preco) || preco <= 0) {
      return avisar({ titulo: 'Informe um preço válido.' });
    }
    const estoque = parseEstoque(form.estoque);
    if (estoque === null) {
      return avisar({ titulo: 'Informe um estoque válido.', mensagem: 'Use apenas números inteiros.' });
    }
    setSalvando(true);
    try {
      const data = {
        nome: form.nome.trim(),
        preco,
        estoque,
        categoria: form.categoria.trim() || undefined,
        codigoBarras: form.codigoBarras.trim() || undefined,
      };
      if (editando) await produtosApi.atualizar(editando.id, data);
      else await produtosApi.criar(data);
      setModalAberto(false);
      carregar();
    } catch (err) {
      await avisar({
        titulo: 'Erro ao salvar produto',
        mensagem: mensagemDeErro(err, 'Erro desconhecido.'),
        tom: 'erro',
      });
    } finally {
      setSalvando(false);
    }
  };

  const excluir = async (produto: Produto) => {
    const confirmou = await confirmar({
      titulo: 'Inativar produto',
      mensagem: `Deseja inativar "${produto.nome}"?`,
      confirmarLabel: 'Inativar',
      tom: 'perigo',
    });
    if (!confirmou) return;

    try {
      await produtosApi.inativar(produto.id);
      carregar();
    } catch (err) {
      await avisar({
        titulo: 'Erro ao inativar produto',
        mensagem: mensagemDeErro(err, 'Erro desconhecido.'),
        tom: 'erro',
      });
    }
  };

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.titulo}>Produtos</Text>
        <TouchableOpacity style={styles.btnNovo} onPress={() => abrirModal()}>
          <Ionicons name="add" size={20} color={colors.white} />
          <Text style={styles.btnNovoLabel}>Novo Produto</Text>
        </TouchableOpacity>
      </View>

      {loading ? <ActivityIndicator color={colors.primary} style={{ flex: 1 }} /> : (
        <FlatList
          data={produtos}
          keyExtractor={p => p.id}
          contentContainerStyle={styles.lista}
          renderItem={({ item }) => (
            <View style={styles.card}>
              <View style={{ flex: 1 }}>
                <Text style={styles.cardNome}>{item.nome}</Text>
                {item.categoria && <Text style={styles.cardCategoria}>{item.categoria}</Text>}
                <View style={styles.cardInfo}>
                  <Text style={styles.cardPreco}>{formatBRL(item.preco)}</Text>
                  <Text style={[styles.cardEstoque, item.estoque < 5 && { color: colors.danger }]}>
                    Estoque: {item.estoque}
                  </Text>
                </View>
              </View>
              <View style={styles.cardAcoes}>
                <TouchableOpacity onPress={() => abrirModal(item)} style={styles.btnAcao}>
                  <Ionicons name="pencil" size={18} color={colors.primary} />
                </TouchableOpacity>
                <TouchableOpacity onPress={() => excluir(item)} style={styles.btnAcao}>
                  <Ionicons name="trash-outline" size={18} color={colors.danger} />
                </TouchableOpacity>
              </View>
            </View>
          )}
          ListEmptyComponent={<Text style={styles.vazio}>Nenhum produto cadastrado.</Text>}
        />
      )}

      {/* Modal de criação/edição */}
      <Modal visible={modalAberto} animationType="slide" transparent>
        <View style={styles.modalOverlay}>
          <View style={styles.modal}>
            <Text style={styles.modalTitulo}>{editando ? 'Editar Produto' : 'Novo Produto'}</Text>

            <Campo label="Nome *" value={form.nome} onChangeText={(v: string) => setForm(f => ({ ...f, nome: v }))} />
            <Campo label="Preço *" value={form.preco} onChangeText={(v: string) => setForm(f => ({ ...f, preco: v }))} keyboardType="decimal-pad" />
            <Campo label="Estoque" value={form.estoque} onChangeText={(v: string) => setForm(f => ({ ...f, estoque: v }))} keyboardType="number-pad" />
            <Campo label="Categoria" value={form.categoria} onChangeText={(v: string) => setForm(f => ({ ...f, categoria: v }))} />

            <View style={{ marginBottom: spacing.md }}>
              <Text style={{ fontSize: fontSize.sm, color: colors.textMuted, marginBottom: spacing.xs }}>Código de barras</Text>
              <View style={{ flexDirection: 'row', gap: spacing.sm }}>
                <TextInput
                  style={{ flex: 1, borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, padding: spacing.md, fontSize: fontSize.md, color: colors.text }}
                  value={form.codigoBarras}
                  onChangeText={(v: string) => setForm(f => ({ ...f, codigoBarras: v }))}
                  placeholder="Digite ou escaneie"
                />
                <TouchableOpacity style={styles.btnScanner} onPress={abrirScanner}>
                  <Ionicons name="barcode-outline" size={22} color={colors.white} />
                </TouchableOpacity>
              </View>
            </View>

            <View style={styles.modalAcoes}>
              <TouchableOpacity style={styles.btnCancelar} onPress={() => setModalAberto(false)}>
                <Text style={styles.btnCancelarLabel}>Cancelar</Text>
              </TouchableOpacity>
              <TouchableOpacity style={styles.btnSalvar} onPress={salvar} disabled={salvando}>
                {salvando ? <ActivityIndicator color={colors.white} size="small" /> : <Text style={styles.btnSalvarLabel}>Salvar</Text>}
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

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
            <Text style={styles.scannerDica}>Aponte a câmera para o código de barras</Text>
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

function Campo({ label, value, onChangeText, keyboardType }: any) {
  return (
    <View style={{ marginBottom: spacing.md }}>
      <Text style={{ fontSize: fontSize.sm, color: colors.textMuted, marginBottom: spacing.xs }}>{label}</Text>
      <TextInput
        style={{ borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, padding: spacing.md, fontSize: fontSize.md, color: colors.text }}
        value={value} onChangeText={onChangeText} keyboardType={keyboardType}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: spacing.lg },
  titulo: { fontSize: fontSize.xxl, fontWeight: '700', color: colors.text },
  btnNovo: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, backgroundColor: colors.primary, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  btnNovoLabel: { color: colors.white, fontWeight: '700', fontSize: fontSize.sm },
  lista: { padding: spacing.lg, paddingTop: 0, gap: spacing.sm },
  card: { flexDirection: 'row', alignItems: 'center', backgroundColor: colors.surface, borderRadius: radius.lg, padding: spacing.md, borderWidth: 1, borderColor: colors.border },
  cardNome: { fontSize: fontSize.md, fontWeight: '700', color: colors.text },
  cardCategoria: { fontSize: fontSize.xs, color: colors.textMuted },
  cardInfo: { flexDirection: 'row', gap: spacing.md, marginTop: spacing.xs },
  cardPreco: { fontSize: fontSize.md, fontWeight: '700', color: colors.primary },
  cardEstoque: { fontSize: fontSize.sm, color: colors.textMuted },
  cardAcoes: { flexDirection: 'row', gap: spacing.sm },
  btnAcao: { padding: spacing.sm },
  vazio: { textAlign: 'center', color: colors.textMuted, marginTop: spacing.xl },
  modalOverlay: { flex: 1, backgroundColor: '#00000060', justifyContent: 'center', padding: spacing.lg },
  modal: { backgroundColor: colors.surface, borderRadius: radius.xl, padding: spacing.lg },
  modalTitulo: { fontSize: fontSize.xl, fontWeight: '700', color: colors.text, marginBottom: spacing.lg },
  modalAcoes: { flexDirection: 'row', gap: spacing.md, marginTop: spacing.md },
  btnCancelar: { flex: 1, padding: spacing.md, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, alignItems: 'center' },
  btnCancelarLabel: { fontSize: fontSize.md, color: colors.text },
  btnSalvar: { flex: 1, padding: spacing.md, borderRadius: radius.md, backgroundColor: colors.primary, alignItems: 'center' },
  btnSalvarLabel: { fontSize: fontSize.md, color: colors.white, fontWeight: '700' },
  btnScanner: { justifyContent: 'center', alignItems: 'center', width: 50, borderRadius: radius.md, backgroundColor: colors.primary },
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
