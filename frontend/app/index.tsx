import { useCallback, useState } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, ScrollView, ActivityIndicator } from 'react-native';
import { useRouter, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { vendasApi } from '../src/api';
import type { Dashboard } from '../src/types';
import { colors, spacing, fontSize, radius } from '../src/theme';

const formatBRL = (v: number) =>
  v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function DashboardScreen() {
  const router = useRouter();
  const [dashboard, setDashboard] = useState<Dashboard | null>(null);
  const [loading, setLoading] = useState(true);

  useFocusEffect(
    useCallback(() => {
      setLoading(true);
      vendasApi.dashboard()
        .then(setDashboard)
        .finally(() => setLoading(false));
    }, [])
  );

  const atalhos = [
    { label: 'Nova Venda', icon: 'cart', color: colors.primary, route: '/venda' },
    { label: 'Produtos', icon: 'cube', color: '#3A86FF', route: '/produtos' },
    { label: 'Fiado', icon: 'people', color: colors.warning, route: '/fiado' },
  ] as const;

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <Text style={styles.titulo}>PDV Mercadinho</Text>
      <Text style={styles.data}>{new Date().toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'long' })}</Text>

      {/* Atalhos */}
      <View style={styles.atalhos}>
        {atalhos.map(({ label, icon, color, route }) => (
          <TouchableOpacity key={label} style={[styles.atalho, { backgroundColor: color }]} onPress={() => router.push(route)}>
            <Ionicons name={icon as any} size={28} color={colors.white} />
            <Text style={styles.atalhoLabel}>{label}</Text>
          </TouchableOpacity>
        ))}
      </View>

      {/* Cards do dia */}
      <Text style={styles.secaoTitulo}>Resumo de Hoje</Text>
      {loading ? (
        <ActivityIndicator color={colors.primary} style={{ marginTop: spacing.lg }} />
      ) : (
        <View style={styles.cards}>
          {/* "Total Vendido" inclui o que saiu fiado; "Recebido" é o que virou caixa.
              Sem os dois lado a lado, a venda fiada aparece somada ao total e ao
              fiado pendente ao mesmo tempo, e o fechamento não bate com a gaveta. */}
          <Card titulo="Total Vendido" valor={formatBRL(dashboard?.totalVendidoHoje ?? 0)} icon="trending-up" cor={colors.primary} />
          <Card titulo="Recebido Hoje" valor={formatBRL(dashboard?.totalRecebidoHoje ?? 0)} icon="cash" cor={colors.success} />
          <Card titulo="Fiado Hoje" valor={formatBRL(dashboard?.totalFiadoHoje ?? 0)} icon="time" cor={colors.warning} />
          <Card titulo="Nº de Vendas" valor={String(dashboard?.totalVendasHoje ?? 0)} icon="receipt" cor="#3A86FF" onPress={() => router.push('/vendas')} />
          <Card titulo="Fiado Pendente" valor={formatBRL(dashboard?.totalFiadoPendente ?? 0)} icon="alert-circle" cor={colors.danger} />
          <Card titulo="Clientes c/ Fiado" valor={String(dashboard?.clientesComFiado ?? 0)} icon="people" cor={colors.warning} />
        </View>
      )}
    </ScrollView>
  );
}

function Card({ titulo, valor, icon, cor, onPress }: { titulo: string; valor: string; icon: string; cor: string; onPress?: () => void }) {
  const Wrapper = onPress ? TouchableOpacity : View;
  return (
    <Wrapper style={styles.card} onPress={onPress}>
      <View style={[styles.cardIcon, { backgroundColor: cor + '20' }]}>
        <Ionicons name={icon as any} size={20} color={cor} />
      </View>
      <Text style={styles.cardValor}>{valor}</Text>
      <Text style={styles.cardTitulo}>{titulo}</Text>
    </Wrapper>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  content: { padding: spacing.lg },
  titulo: { fontSize: fontSize.xxl, fontWeight: '700', color: colors.text },
  data: { fontSize: fontSize.sm, color: colors.textMuted, marginTop: spacing.xs, marginBottom: spacing.xl, textTransform: 'capitalize' },
  atalhos: { flexDirection: 'row', gap: spacing.md, marginBottom: spacing.xl },
  atalho: { flex: 1, borderRadius: radius.lg, padding: spacing.md, alignItems: 'center', gap: spacing.sm },
  atalhoLabel: { color: colors.white, fontWeight: '600', fontSize: fontSize.sm },
  secaoTitulo: { fontSize: fontSize.lg, fontWeight: '700', color: colors.text, marginBottom: spacing.md },
  cards: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md },
  card: { backgroundColor: colors.surface, borderRadius: radius.lg, padding: spacing.md, flex: 1, minWidth: 140, gap: spacing.xs },
  cardIcon: { width: 36, height: 36, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  cardValor: { fontSize: fontSize.xl, fontWeight: '700', color: colors.text },
  cardTitulo: { fontSize: fontSize.xs, color: colors.textMuted },
});
