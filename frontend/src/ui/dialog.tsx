import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react';
import { Modal, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { colors, fontSize, radius, spacing } from '../theme';

/**
 * Diálogos do PDV.
 *
 * Substitui `Alert.alert`, que é um no-op em react-native-web — no navegador
 * (o modo recomendado do PDV) os botões de confirmar venda simplesmente não
 * faziam nada. Esta implementação é uma Modal do próprio app, então se comporta
 * igual em web, Android e iOS e segue o tema.
 *
 * Uso:
 *   const { confirmar, avisar } = useDialog();
 *   if (await confirmar({ titulo: 'Finalizar?', mensagem: 'Total: R$ 12,00' })) { ... }
 *   await avisar({ titulo: 'Venda finalizada!' });
 */

type Tom = 'normal' | 'perigo' | 'sucesso' | 'erro';

interface OpcoesConfirmacao {
  titulo: string;
  mensagem?: string;
  confirmarLabel?: string;
  cancelarLabel?: string;
  tom?: Tom;
}

interface OpcoesAviso {
  titulo: string;
  mensagem?: string;
  tom?: Tom;
}

interface DialogContextValue {
  /** Resolve `true` se o usuário confirmou, `false` se cancelou. */
  confirmar: (opcoes: OpcoesConfirmacao) => Promise<boolean>;
  /** Mensagem com um único botão. Resolve quando o usuário fecha. */
  avisar: (opcoes: OpcoesAviso) => Promise<void>;
}

const DialogContext = createContext<DialogContextValue | null>(null);

interface EstadoDialogo extends OpcoesConfirmacao {
  tipo: 'confirmar' | 'avisar';
}

export function DialogProvider({ children }: { children: React.ReactNode }) {
  const [estado, setEstado] = useState<EstadoDialogo | null>(null);
  // Guarda o resolve da Promise em aberto para o botão pressionado poder liberá-la.
  const resolverRef = useRef<((confirmou: boolean) => void) | null>(null);

  const abrir = useCallback((proximo: EstadoDialogo) => {
    // Se já houver um diálogo aberto, o anterior é resolvido como cancelado
    // para que nenhum `await` fique pendurado para sempre.
    resolverRef.current?.(false);
    setEstado(proximo);
    return new Promise<boolean>(resolve => { resolverRef.current = resolve; });
  }, []);

  const fechar = useCallback((confirmou: boolean) => {
    const resolver = resolverRef.current;
    resolverRef.current = null;
    setEstado(null);
    resolver?.(confirmou);
  }, []);

  const valor = useMemo<DialogContextValue>(() => ({
    confirmar: opcoes => abrir({ ...opcoes, tipo: 'confirmar' }),
    avisar: async opcoes => { await abrir({ ...opcoes, tipo: 'avisar' }); },
  }), [abrir]);

  const corConfirmar =
    estado?.tom === 'perigo' || estado?.tom === 'erro' ? colors.danger
    : estado?.tom === 'sucesso' ? colors.success
    : colors.primary;

  return (
    <DialogContext.Provider value={valor}>
      {children}

      <Modal
        visible={estado !== null}
        transparent
        animationType="fade"
        // Botão voltar do Android / Esc: equivale a cancelar.
        onRequestClose={() => fechar(false)}
      >
        <View style={styles.overlay}>
          <View style={styles.caixa}>
            <Text style={styles.titulo}>{estado?.titulo}</Text>
            {estado?.mensagem ? <Text style={styles.mensagem}>{estado.mensagem}</Text> : null}

            <View style={styles.botoes}>
              {estado?.tipo === 'confirmar' && (
                <TouchableOpacity style={[styles.btn, styles.btnCancelar]} onPress={() => fechar(false)}>
                  <Text style={styles.btnCancelarLabel}>{estado.cancelarLabel ?? 'Cancelar'}</Text>
                </TouchableOpacity>
              )}
              <TouchableOpacity
                style={[styles.btn, { backgroundColor: corConfirmar }]}
                onPress={() => fechar(true)}
              >
                <Text style={styles.btnConfirmarLabel}>
                  {estado?.tipo === 'confirmar' ? (estado.confirmarLabel ?? 'Confirmar') : 'OK'}
                </Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </DialogContext.Provider>
  );
}

export function useDialog(): DialogContextValue {
  const ctx = useContext(DialogContext);
  if (!ctx) throw new Error('useDialog precisa estar dentro de <DialogProvider>.');
  return ctx;
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1, backgroundColor: '#00000070',
    alignItems: 'center', justifyContent: 'center', padding: spacing.xl,
  },
  caixa: {
    width: '100%', maxWidth: 420,
    backgroundColor: colors.surface, borderRadius: radius.xl, padding: spacing.xl,
    shadowColor: '#000', shadowOpacity: 0.15, shadowRadius: 16, elevation: 8,
  },
  titulo: { fontSize: fontSize.xl, fontWeight: '700', color: colors.text },
  mensagem: { fontSize: fontSize.md, color: colors.textMuted, marginTop: spacing.sm, lineHeight: 22 },
  botoes: { flexDirection: 'row', gap: spacing.md, marginTop: spacing.xl },
  btn: { flex: 1, borderRadius: radius.md, paddingVertical: spacing.md, alignItems: 'center' },
  btnCancelar: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border },
  btnCancelarLabel: { fontSize: fontSize.md, color: colors.textMuted, fontWeight: '600' },
  btnConfirmarLabel: { fontSize: fontSize.md, color: colors.white, fontWeight: '700' },
});
