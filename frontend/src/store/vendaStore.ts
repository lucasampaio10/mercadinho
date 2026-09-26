import AsyncStorage from '@react-native-async-storage/async-storage';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import type { Cliente, FormaPagamento, ItemVenda, Produto, Venda } from '../types';
import { mensagemDeErro, vendasApi } from '../api';

/**
 * Estado da venda em andamento.
 *
 * Duas regras governam este store:
 *
 * 1. **O servidor é a fonte de verdade do carrinho.** Toda mutação devolve a venda
 *    inteira e substitui `itens`/`total`. O front não soma preços em `number` (float)
 *    para depois cobrar outro valor — o `total` exibido é o mesmo `decimal` que o
 *    backend vai lançar no fiado.
 *
 * 2. **A venda sobrevive a um refresh.** Só o `vendaId` precisa durar; `itens`/`total`
 *    são persistidos apenas para a tela pintar algo imediatamente, e são reconciliados
 *    contra o servidor assim que `restaurarVenda` responde.
 *
 * 3. **As operações de carrinho são serializadas** — ver `enfileirar`.
 */
interface VendaStore {
  vendaId: string | null;
  itens: ItemVenda[];
  /** Total calculado pelo backend. Nunca recalcule isto no cliente. */
  total: number;
  clienteSelecionado: Cliente | null;
  formaPagamento: FormaPagamento | null;
  /** Nota opcional do operador (ex: "sem troco", "entregar depois"). Vale para as 3 formas de pagamento. */
  observacao: string;
  loading: boolean;
  error: string | null;
  /** `false` até o storage ser lido — evita iniciar uma venda por cima de outra aberta. */
  hidratado: boolean;

  // Actions
  iniciarVenda: () => Promise<void>;
  restaurarVenda: () => Promise<void>;
  adicionarItem: (produto: Produto, quantidade?: number) => Promise<void>;
  /** Define a quantidade exata de um item já no carrinho. Zero ou menos remove o item. */
  alterarQuantidade: (produtoId: string, quantidade: number) => Promise<void>;
  removerItem: (produtoId: string) => Promise<void>;
  /** Descarta no servidor a venda aberta e esvazia o carrinho. */
  cancelarVenda: () => Promise<void>;
  selecionarCliente: (cliente: Cliente | null) => void;
  selecionarFormaPagamento: (forma: FormaPagamento) => void;
  definirObservacao: (texto: string) => void;
  finalizarVenda: () => Promise<Venda | null>;
  limparErro: () => void;
  limpar: () => void;
}

const ESTADO_INICIAL = {
  vendaId: null,
  itens: [] as ItemVenda[],
  total: 0,
  clienteSelecionado: null,
  formaPagamento: null,
  observacao: '',
  loading: false,
  error: null,
};

/**
 * Fila serial das operações de carrinho.
 *
 * Sem ela, dois cliques rápidos com `vendaId === null` disparam dois
 * `POST /api/vendas` concorrentes: o segundo sobrescreve o `vendaId`, o item A fica
 * na venda A e o item B na venda B. A tela mostra os dois itens, mas o backend
 * finaliza só uma das vendas — o cliente leva produto que não foi cobrado, ou o
 * fiado é lançado pela metade.
 *
 * Serializar também garante que o estado exibido seja o da última operação enviada,
 * e não o da resposta que por acaso voltou por último da rede.
 */
let fila: Promise<unknown> = Promise.resolve();

function enfileirar<T>(operacao: () => Promise<T>): Promise<T> {
  // `then(op, op)` encadeia a próxima operação tenha a anterior dado certo ou não.
  const proxima = fila.then(operacao, operacao);
  // As operações tratam os próprios erros; este catch só impede que uma rejeição
  // inesperada trave a fila para todas as chamadas seguintes.
  fila = proxima.catch(() => undefined);
  return proxima;
}

export const useVendaStore = create<VendaStore>()(
  persist(
    (set, get) => {
      // ── Implementações internas ─────────────────────────────────────────────
      // Não passam pela fila: são chamadas de dentro de operações já enfileiradas,
      // e reenfileirar aqui faria a fila esperar por si mesma (deadlock).

      const abrirVenda = async () => {
        set({ loading: true, error: null });
        try {
          const vendaId = await vendasApi.iniciar();
          set({ vendaId, itens: [], total: 0, loading: false });
        } catch (err) {
          set({ error: mensagemDeErro(err, 'Erro ao iniciar venda.'), loading: false });
        }
      };

      /**
       * Recarrega do servidor a venda persistida. Se ela não existe mais ou já foi
       * fechada, descarta o estado local em vez de deixar o caixa operar um carrinho
       * fantasma.
       */
      const recarregarVenda = async () => {
        const { vendaId } = get();
        if (!vendaId) return;

        set({ loading: true });
        try {
          const venda = await vendasApi.obter(vendaId);
          if (venda.status !== 'Aberta') {
            set({ ...ESTADO_INICIAL });
            return;
          }
          set({ itens: venda.itens, total: venda.total, loading: false, error: null });
        } catch (err) {
          // 404: a venda sumiu do servidor — o carrinho local não vale mais nada.
          if ((err as { response?: { status?: number } })?.response?.status === 404) {
            set({ ...ESTADO_INICIAL });
            return;
          }
          // Falha de rede: preserva o carrinho e avisa, em vez de apagar o que o caixa digitou.
          set({
            loading: false,
            error: mensagemDeErro(err, 'Não foi possível recuperar a venda em andamento.'),
          });
        }
      };

      return {
        ...ESTADO_INICIAL,
        hidratado: false,

        iniciarVenda: () => enfileirar(abrirVenda),

        restaurarVenda: () => enfileirar(recarregarVenda),

        /**
         * A venda só é aberta no servidor quando o primeiro item entra — abrir ao
         * montar a tela deixaria uma venda vazia órfã a cada visita à aba.
         */
        adicionarItem: (produto, quantidade = 1) => enfileirar(async () => {
          set({ error: null });
          try {
            let id = get().vendaId;
            if (!id) {
              await abrirVenda();
              id = get().vendaId;
              if (!id) return; // abrirVenda já registrou o erro
            }

            const venda = await vendasApi.adicionarItem(id, produto.id, quantidade);
            set({ itens: venda.itens, total: venda.total });
          } catch (err) {
            // A mensagem do backend ("Estoque insuficiente de "Arroz". Disponível: 3.")
            // é o que o operador precisa ler — não a substitua por um texto genérico.
            set({ error: mensagemDeErro(err, 'Erro ao adicionar item.') });
          }
        }),

        /**
         * Corrigir uma quantidade não deve custar remover o item e refazê-lo.
         * Chegar a zero é a mesma intenção que remover, então cai no mesmo endpoint —
         * é o que faz o botão "−" funcionar até o fim sem um caso especial na tela.
         */
        alterarQuantidade: (produtoId, quantidade) => enfileirar(async () => {
          const { vendaId } = get();
          if (!vendaId) return;

          set({ error: null });
          try {
            const venda = quantidade <= 0
              ? await vendasApi.removerItem(vendaId, produtoId)
              : await vendasApi.alterarQuantidade(vendaId, produtoId, quantidade);
            set({ itens: venda.itens, total: venda.total });
          } catch (err) {
            set({ error: mensagemDeErro(err, 'Erro ao alterar a quantidade.') });
          }
        }),

        removerItem: (produtoId) => enfileirar(async () => {
          const { vendaId } = get();
          if (!vendaId) return;

          set({ error: null });
          try {
            const venda = await vendasApi.removerItem(vendaId, produtoId);
            set({ itens: venda.itens, total: venda.total });
          } catch (err) {
            set({ error: mensagemDeErro(err, 'Erro ao remover item.') });
          }
        }),

        /**
         * Fecha no servidor a venda aberta. Sem isso, todo carrinho abandonado deixa
         * uma venda `Aberta` órfã no banco para sempre.
         */
        cancelarVenda: () => enfileirar(async () => {
          const { vendaId } = get();
          if (!vendaId) {
            set({ ...ESTADO_INICIAL });
            return;
          }

          set({ loading: true, error: null });
          try {
            await vendasApi.cancelar(vendaId);
            set({ ...ESTADO_INICIAL });
          } catch (err) {
            set({ error: mensagemDeErro(err, 'Erro ao cancelar a venda.'), loading: false });
          }
        }),

        selecionarCliente: (cliente) => set({ clienteSelecionado: cliente }),
        selecionarFormaPagamento: (forma) => set({ formaPagamento: forma }),
        definirObservacao: (texto) => set({ observacao: texto }),

        /**
         * Finaliza a venda e devolve a venda gravada — quem chamou deve exibir
         * `venda.total`, que é o valor efetivamente cobrado/lançado no fiado.
         * Retorna `null` em caso de falha (a mensagem fica em `error`).
         *
         * Enfileirada junto das demais: um item clicado no último instante entra na
         * venda antes do fechamento, em vez de chegar depois e ficar órfão.
         */
        finalizarVenda: () => enfileirar(async () => {
          const { vendaId, formaPagamento, clienteSelecionado, observacao } = get();
          if (!vendaId || !formaPagamento) {
            set({ error: 'Selecione a forma de pagamento antes de finalizar.' });
            return null;
          }

          set({ loading: true, error: null });
          try {
            const venda = await vendasApi.finalizar(vendaId, formaPagamento, clienteSelecionado?.id, observacao.trim() || undefined);
            set({ ...ESTADO_INICIAL });
            return venda;
          } catch (err) {
            set({ error: mensagemDeErro(err, 'Erro ao finalizar venda.'), loading: false });
            return null;
          }
        }),

        limparErro: () => set({ error: null }),

        limpar: () => set({ ...ESTADO_INICIAL }),
      };
    },
    {
      name: 'pdv-venda-em-andamento',
      storage: createJSONStorage(() => AsyncStorage),
      // `loading`/`error` são efêmeros; não fazem sentido atravessar um refresh.
      partialize: (s) => ({
        vendaId: s.vendaId,
        itens: s.itens,
        total: s.total,
        clienteSelecionado: s.clienteSelecionado,
        observacao: s.observacao,
      }),
      // Roda depois que o storage foi lido — inclusive quando não havia nada salvo.
      onRehydrateStorage: () => () => {
        useVendaStore.setState({ hidratado: true });
      },
    }
  )
);
