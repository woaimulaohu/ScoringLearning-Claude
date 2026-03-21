import axios from 'axios';
import { ElMessage } from 'element-plus';
import camelcaseKeys from 'camelcase-keys';

const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api/v1',
  timeout: 30000,
});

// 响应拦截器：将后端 PascalCase 字段名转换为前端 camelCase
http.interceptors.response.use(
  (response) => {
    if (response.data && typeof response.data === 'object') {
      response.data = camelcaseKeys(response.data, { deep: true });
    }
    return response;
  },
  (error) => {
    const msg = error.response?.data?.message || error.message || '请求失败';
    ElMessage.error(msg);
    return Promise.reject(error);
  }
);

export default http;
