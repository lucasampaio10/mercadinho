import axios from 'axios';
import type { Cliente, Dashboard, FormaPagamento, HistoricoFiado, Produto, Venda } from '../types';

// Em produção desktop, a API roda local. Ajuste a porta se necessário.
const BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000';

/**
 * Chave compartilhada exigida pela API (header `X-API-Key`).
 *
 * O PDV é exposto na rede local para o celular do balcão, e "rede local" não é
 * fronteira de segurança: sem a chave, qualquer aparelho no Wi-Fi da loja acessa
 * os mesmos endpoints. Precisa ser igual ao `API_KEY` do backend.
 */
const API_KEY = process.env.EXPO_PUBLIC_API_KEY ?? '';

/**
 * 60s, não os 10s de uma API que nunca dorme: o plano free do Render suspende o
 * serviço após ~15min sem tráfego, e a primeira requisição depois disso paga o
 * cold start — a Render cita "até 50s", só que na prática já foi medido acima
 * disso quando o Postgres do Neon também estava suspenso (o app espera a conexão
 * com o banco antes de responder qualquer coisa). Com um timeout curto, essa
 * primeira chamada do dia sempre falhava antes da API sequer acordar.
 */
const api = axios.create({
  baseURL: BASE_URL,
  timeout: 60_000,
  headers: {
    'Content-Type': 'application/json',
    ...(API_KEY ? { 'X-API-Key': API_KEY } : {}),
  },
});

/**
 * Uma segunda tentativa automática quando a primeira falha por timeout ou sem
 * resposta nenhuma — não para erros de negócio (400/401/etc), que já vieram do
 * backend e repetir não muda nada. O cold start do Render é o caso que isso
 * cobre: a 1ª chamada "acorda" o servidor (e pode estourar o timeout antes dele
 * responder), a 2ª já encontra tudo de pé e volta rápido. Evita o operador
 * precisar apertar "Tentar novamente" manualmente logo de cara.
 */
api.interceptors.response.use(undefined, async (err) => {
  const config = err?.config as (typeof err.config & { _retryApósColdStart?: boolean }) | undefined;
  const semResposta = axios.isAxiosError(err) && !err.response;
  if (config && semResposta && !config._retryApósColdStart) {
    config._retryApósColdStart = true;
    return api(config);
  }
  return Promise.reject(err);
});

/**
 * Extrai a mensagem que o backend enviou no corpo do erro.
 *
 * A API responde `{ error: "Estoque insuficiente. Disponível: 3." }` — é a única
 * informação que diz ao operador *o que* deu errado. Trocar isso por um texto
 * genérico ("Erro ao adicionar item") esconde justamente o que ele precisa saber.
 * O fallback só entra quando não há resposta (rede fora, timeout).
 */
export function mensagemDeErro(err: unknown, fallback: string): string {
  if (axios.isAxiosError(err)) {
    const doBackend = (err.response?.data as { error?: string } | undefined)?.error;
    if (doBackend) return doBackend;

    if (err.response?.status === 401) {
      return 'A API recusou a chave de acesso. Confira EXPO_PUBLIC_API_KEY no .env.';
    }
    if (err.code === 'ECONNABORTED') {
      return 'A API demorou demais para responder. Se o sistema estava parado há um tempo, o servidor pode estar iniciando — tente de novo em alguns segundos.';
    }
    if (!err.response) return 'Sem conexão com a API. Verifique sua internet ou se o backend está rodando.';
  }
  return fallback;
}

// ── Produtos ──────────────────────────────────────────────────────────────────

export const produtosApi = {
  listar: () => api.get<Produto[]>('/api/produtos').then(r => r.data),

  /**
   * `signal` permite abortar a busca quando o termo muda: sem isso, a resposta de
   * uma consulta antiga pode chegar depois e sobrescrever a lista atual.
   */
  buscar: (nome: string, signal?: AbortSignal) =>
    api.get<Produto[]>('/api/produtos/buscar', { params: { nome }, signal }).then(r => r.data),

  /** Busca exata pelo código de barras lido na câmera. `null` quando não encontra. */
  buscarPorCodigoBarras: (codigo: string) =>
    api.get<Produto>(`/api/produtos/codigo-barras/${encodeURIComponent(codigo)}`)
      .then(r => r.data)
      .catch(err => {
        if (axios.isAxiosError(err) && err.response?.status === 404) return null;
        throw err;
      }),

  criar: (data: { nome: string; preco: number; estoque: number; categoria?: string; codigoBarras?: string }) =>
    api.post<Produto>('/api/produtos', data).then(r => r.data),

  atualizar: (id: string, data: { nome: string; preco: number; estoque: number; categoria?: string; codigoBarras?: string }) =>
    api.put(`/api/produtos/${id}`, data),

  inativar: (id: string) => api.delete(`/api/produtos/${id}`),
};

// ── Vendas ────────────────────────────────────────────────────────────────────

export const vendasApi = {
  dashboard: () => api.get<Dashboard>('/api/vendas/dashboard').then(r => r.data),

  /** Vendas finalizadas hoje, mais recentes primeiro. */
  listarHoje: () => api.get<Venda[]>('/api/vendas/hoje').then(r => r.data),

  iniciar: () => api.post<{ vendaId: string }>('/api/vendas').then(r => r.data.vendaId),

  /** Estado atual da venda no servidor — usado para restaurar o carrinho após refresh. */
  obter: (vendaId: string) =>
    api.get<Venda>(`/api/vendas/${vendaId}`).then(r => r.data),

  /** Retorna a venda inteira; o carrinho local deve ser substituído por ela. */
  adicionarItem: (vendaId: string, produtoId: string, quantidade: number) =>
    api.post<Venda>(`/api/vendas/${vendaId}/itens`, { produtoId, quantidade }).then(r => r.data),

  /**
   * Define a quantidade exata do item — corrigir "digitei 10, era 1" sem remover e
   * refazer. Retorna a venda inteira; o carrinho local deve ser substituído por ela.
   */
  alterarQuantidade: (vendaId: string, produtoId: string, quantidade: number) =>
    api.put<Venda>(`/api/vendas/${vendaId}/itens/${produtoId}`, { quantidade }).then(r => r.data),

  /** Retorna a venda inteira; o carrinho local deve ser substituído por ela. */
  removerItem: (vendaId: string, produtoId: string) =>
    api.delete<Venda>(`/api/vendas/${vendaId}/itens/${produtoId}`).then(r => r.data),

  /** Descarta uma venda aberta — evita deixar carrinho abandonado órfão no banco. */
  cancelar: (vendaId: string) =>
    api.post<void>(`/api/vendas/${vendaId}/cancelar`).then(r => r.data),

  finalizar: (vendaId: string, formaPagamento: FormaPagamento, clienteId?: string, observacao?: string) =>
    api.post<Venda>(`/api/vendas/${vendaId}/finalizar`, { formaPagamento, clienteId, observacao }).then(r => r.data),
};

// ── Clientes ──────────────────────────────────────────────────────────────────

export const clientesApi = {
  /** Todos os clientes ativos (para selecionar no fiado). */
  listar: () => api.get<Cliente[]>('/api/clientes').then(r => r.data),

  /** Apenas clientes com fiado pendente (para a tela de fiado). */
  listarComFiado: () => api.get<Cliente[]>('/api/clientes/fiado').then(r => r.data),

  criar: (data: { nome: string; telefone?: string }) =>
    api.post<Cliente>('/api/clientes', data).then(r => r.data),

  historico: (clienteId: string) =>
    api.get<HistoricoFiado>(`/api/clientes/${clienteId}/fiado`).then(r => r.data),

  registrarPagamento: (clienteId: string, valor: number) =>
    api.post(`/api/clientes/${clienteId}/pagamento`, { valor }),
};
