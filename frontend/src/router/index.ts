import { createRouter, createWebHistory } from 'vue-router';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      redirect: '/dashboard',
    },
    {
      path: '/dashboard',
      component: () => import('@/views/Dashboard.vue'),
      meta: { title: '仪表板' },
    },
    {
      path: '/tasks',
      component: () => import('@/views/TaskList.vue'),
      meta: { title: '任务列表' },
    },
    {
      path: '/tasks/:id',
      component: () => import('@/views/TaskDetail.vue'),
      meta: { title: '任务详情' },
    },
  ],
});

router.beforeEach((to) => {
  document.title = `${to.meta.title || ''} - ClaudeCode 评分系统`;
});

export default router;
