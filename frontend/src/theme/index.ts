// Paleta de cores do PDV — simples, legível, sem distrações
export const colors = {
  primary: '#2D6A4F',       // verde escuro — ação principal
  primaryLight: '#52B788',  // verde claro — hover/destaque
  danger: '#E63946',        // vermelho — cancelar / fiado em atraso
  warning: '#F4A261',       // laranja — atenção
  success: '#52B788',       // verde — confirmação
  background: '#F8F9FA',    // cinza muito claro — fundo
  surface: '#FFFFFF',       // branco — cards
  border: '#DEE2E6',        // cinza claro — bordas
  text: '#212529',          // quase preto — texto principal
  textMuted: '#6C757D',     // cinza — texto secundário
  white: '#FFFFFF',
} as const;

export const spacing = {
  xs: 4, sm: 8, md: 16, lg: 24, xl: 32, xxl: 48,
} as const;

export const radius = {
  sm: 4, md: 8, lg: 12, xl: 16, full: 999,
} as const;

export const fontSize = {
  xs: 11, sm: 13, md: 15, lg: 17, xl: 20, xxl: 24, xxxl: 30,
} as const;
