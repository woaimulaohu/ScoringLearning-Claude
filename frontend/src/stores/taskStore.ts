import { defineStore } from 'pinia';
import { ref } from 'vue';
import { tasksApi } from '@/api/tasks';
import type { Task, ReviewRequest } from '@/types';

export const useTaskStore = defineStore('task', () => {
  const tasks = ref<Task[]>([]);
  const currentTask = ref<Task | null>(null);
  const total = ref(0);
  const loading = ref(false);
  const currentPage = ref(1);
  const pageSize = ref(20);
  const statusFilter = ref<string>('');

  const fetchTasks = async () => {
    loading.value = true;
    try {
      const res = await tasksApi.getList({
        status: statusFilter.value || undefined,
        page: currentPage.value,
        pageSize: pageSize.value,
      });
      tasks.value = res.data.data;
      total.value = res.data.total;
    } finally {
      loading.value = false;
    }
  };

  const fetchTask = async (id: string) => {
    loading.value = true;
    try {
      const res = await tasksApi.getById(id);
      currentTask.value = res.data;
    } finally {
      loading.value = false;
    }
  };

  const reviewTask = async (id: string, data: ReviewRequest) => {
    const res = await tasksApi.review(id, data);
    currentTask.value = res.data;
    // 更新列表中的任务
    const idx = tasks.value.findIndex((t) => t.id === id);
    if (idx !== -1) {
      tasks.value[idx] = res.data;
    }
  };

  return {
    tasks,
    currentTask,
    total,
    loading,
    currentPage,
    pageSize,
    statusFilter,
    fetchTasks,
    fetchTask,
    reviewTask,
  };
});
