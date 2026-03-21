/**
 * 将 UTC 时间字符串格式化为本地时间显示
 * 后端返回带 Z 后缀的 ISO 8601 字符串，浏览器 new Date() 会自动转本地时区
 */
export function formatLocalTime(utcStr: string | null | undefined): string {
  if (!utcStr) return '-';
  return new Date(utcStr).toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
}

export function formatLocalDate(utcStr: string | null | undefined): string {
  if (!utcStr) return '-';
  return new Date(utcStr).toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  });
}
