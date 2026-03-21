import { defineStore } from 'pinia';
import { ref, computed } from 'vue';

/**
 * 认证 Store（预留）
 * 后续可扩展为完整的用户认证管理
 */
export const useAuthStore = defineStore('auth', () => {
  const token = ref<string>('');
  const username = ref<string>('');

  const isAuthenticated = computed(() => !!token.value);

  const login = async (_username: string, _password: string) => {
    // TODO: 实现登录逻辑
    // const res = await authApi.login({ username: _username, password: _password });
    // token.value = res.data.token;
    // username.value = _username;
    console.warn('Auth store is a placeholder. Login not implemented yet.');
  };

  const logout = () => {
    token.value = '';
    username.value = '';
  };

  return {
    token,
    username,
    isAuthenticated,
    login,
    logout,
  };
});
