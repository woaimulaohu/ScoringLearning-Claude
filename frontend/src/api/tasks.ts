import http from './http';
import type { Task, PagedResult, ReviewRequest, Lesson, ErrorPattern } from '@/types';

export const tasksApi = {
  /** 获取任务列表 */
  getList: (params: { status?: string; page?: number; pageSize?: number } = {}) =>
    http.get<PagedResult<Task>>('/tasks', { params }),

  /** 获取任务详情 */
  getById: (id: string) =>
    http.get<Task>(`/tasks/${id}`),

  /** 提交审查修正 */
  review: (id: string, data: ReviewRequest) =>
    http.put<Task>(`/tasks/${id}/review`, data),

  /** 获取任务关联教训 */
  getLessons: (id: string) =>
    http.get<Lesson[]>(`/tasks/${id}/lessons`),

  /** 添加教训 */
  addLesson: (id: string, lesson: Omit<Lesson, 'id' | 'createdAt'>) =>
    http.post<Lesson>(`/tasks/${id}/lessons`, lesson),

  /** 获取高频错误模式 */
  getErrorPatterns: (limit = 20) =>
    http.get<ErrorPattern[]>('/analytics/error-patterns', { params: { limit } }),

  /** 获取统计摘要 */
  getSummary: () =>
    http.get<{ total: number; needsReview: number; averageScore: number }>('/analytics/summary'),
};
