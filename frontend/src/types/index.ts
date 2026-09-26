export interface Produto {
  id: string;
  nome: string;
  categoria?: string;
  codigoBarras?: string;
  preco: number;
  estoque: number;
  ativo: boolean;
  createdAt: string;
}


export interface Cliente {
  id: string;
  nome: string;
  telefone?: string;
  saldoFiado: number;
  ativo: boolean;
}

export interface ItemFiado {
  id: string;
  vendaId: string;
  valor: number;
  valorPago: number;
  saldo: number;
  pago: boolean;
  pagoEm?: string;
  createdAt: string;
}

export interface Pagamento {
  id: string;
  valor: number;
  createdAt: string;
}

/** Extrato de fiado do cliente — resposta de `GET /api/clientes/{id}/fiado`. */
export interface HistoricoFiado {
  cliente: Cliente;
  historico: ItemFiado[];
  pagamentos: Pagamento[];
}

export interface Venda {
  id: string;
  status: 'Aberta' | 'Finalizada' | 'Cancelada';
  formaPagamento?: 'Dinheiro' | 'Cartao' | 'Fiado';
  clienteId?: string;
  /** Só vem preenchido quando o backend já resolveu o cliente (ex: listagem de vendas). */
  clienteNome?: string;
  total: number;
  itens: ItemVenda[];
  observacao?: string;
  createdAt: string;
}

export interface ItemVenda {
  produtoId: string;
  nomeProduto: string;
  precoUnitario: number;
  quantidade: number;
  subtotal: number;
}

export interface Dashboard {
  /** Giro do dia: tudo que saiu do balcão, inclusive o que saiu fiado. */
  totalVendidoHoje: number;
  /** O que virou caixa de verdade (dinheiro + cartão). */
  totalRecebidoHoje: number;
  /** O que saiu fiado hoje — já está contido em `totalVendidoHoje`. */
  totalFiadoHoje: number;
  totalVendasHoje: number;
  /** Saldo devedor acumulado de todos os clientes, não só o de hoje. */
  totalFiadoPendente: number;
  clientesComFiado: number;
}

export type FormaPagamento = 'Dinheiro' | 'Cartao' | 'Fiado';
