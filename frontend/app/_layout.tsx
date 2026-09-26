import { Tabs } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { colors } from '../src/theme';
import { DialogProvider } from '../src/ui/dialog';

export default function RootLayout() {
  const insets = useSafeAreaInsets();

  return (
    // DialogProvider envolve tudo: as telas usam useDialog() no lugar de Alert.alert,
    // que não funciona na web.
    <DialogProvider>
      <Tabs
        screenOptions={{
          tabBarActiveTintColor: colors.primary,
          tabBarInactiveTintColor: colors.textMuted,
          tabBarStyle: {
            backgroundColor: colors.surface,
            borderTopColor: colors.border,
            height: 56 + insets.bottom,
            paddingBottom: insets.bottom,
            paddingTop: 8,
          },
          headerStyle: { backgroundColor: colors.surface },
          headerTintColor: colors.text,
          headerTitleStyle: { fontWeight: '700' },
        }}
      >
        <Tabs.Screen
          name="index"
          options={{
            title: 'Início',
            headerShown: false,
            tabBarIcon: ({ color, size }) => (
              <Ionicons name="home" size={size} color={color} />
            ),
          }}
        />
        <Tabs.Screen
          name="venda"
          options={{
            title: 'Venda',
            headerTitle: 'Nova Venda',
            tabBarIcon: ({ color, size }) => (
              <Ionicons name="cart" size={size} color={color} />
            ),
          }}
        />
        <Tabs.Screen
          name="produtos"
          options={{
            title: 'Produtos',
            headerTitle: 'Cadastro de Produtos',
            tabBarIcon: ({ color, size }) => (
              <Ionicons name="cube" size={size} color={color} />
            ),
          }}
        />
        <Tabs.Screen
          name="fiado/index"
          options={{
            title: 'Fiado',
            headerShown: false,
            tabBarIcon: ({ color, size }) => (
              <Ionicons name="people" size={size} color={color} />
            ),
          }}
        />
        {/* Rotas ocultas da tab bar */}
        <Tabs.Screen name="fiado/[id]" options={{ href: null, headerShown: false }} />
        <Tabs.Screen name="fiado/selecionar" options={{ href: null, headerShown: false }} />
        <Tabs.Screen name="vendas" options={{ href: null, headerShown: false }} />
      </Tabs>
    </DialogProvider>
  );
}
