<script setup lang="ts">
import { onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useTaskStore } from '@/stores/taskStore';
import { ElMessage } from 'element-plus';
import { DocumentCopy } from '@element-plus/icons-vue';
import { formatLocalTime } from '@/utils/datetime';

const router = useRouter();
const store = useTaskStore();

const copyTaskId = (id: string, event: Event) => {
  event.stopPropagation(); // 阻止触发行点击跳转
  navigator.clipboard.writeText(id).then(() => {
    ElMessage.success('任务 ID 已复制');
  }).catch(() => {
    ElMessage.error('复制失败');
  });
};

const statusOptions = [
  { label: '全部', value: '' },
  { label: '待处理', value: 'pending' },
  { label: '已评分', value: 'scored' },
  { label: '待审查', value: 'needs_review' },
  { label: '已审查', value: 'reviewed' },
];

const statusTagType = (status: string) => {
  const map: Record<string, string> = {
    pending: 'info',
    scored: 'success',
    needs_review: 'danger',
    reviewed: 'warning',
  };
  return map[status] || 'info';
};

const statusLabel = (status: string) => {
  const map: Record<string, string> = {
    pending: '待处理',
    scored: '已评分',
    needs_review: '待审查',
    reviewed: '已审查',
  };
  return map[status] || status;
};

const onRowClick = (row: { id: string }) => router.push(`/tasks/${row.id}`);

const onPageChange = (page: number) => {
  store.currentPage = page;
  store.fetchTasks();
};

watch(
  () => store.statusFilter,
  () => {
    store.currentPage = 1;
    store.fetchTasks();
  }
);

onMounted(() => store.fetchTasks());
</script>

<template>
  <div>
    <!-- 筛选栏 -->
    <el-card style="margin-bottom: 16px;">
      <el-form inline>
        <el-form-item label="状态筛选">
          <el-select
            v-model="store.statusFilter"
            placeholder="全部"
            style="width: 150px;"
          >
            <el-option
              v-for="opt in statusOptions"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button @click="store.fetchTasks()" :loading="store.loading">
            刷新
          </el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 任务表格 -->
    <el-card>
      <el-table
        :data="store.tasks"
        v-loading="store.loading"
        @row-click="onRowClick"
        row-class-name="cursor-pointer"
        style="width: 100%;"
      >
        <el-table-column label="任务ID" width="320" show-overflow-tooltip>
          <template #default="{ row }">
            <span style="margin-right: 4px;">{{ row.id }}</span>
            <el-button
              size="small"
              :icon="DocumentCopy"
              circle
              text
              @click="copyTaskId(row.id, $event)"
              title="复制任务 ID"
            />
          </template>
        </el-table-column>
        <el-table-column label="描述" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.description.slice(0, 60) }}
          </template>
        </el-table-column>
        <el-table-column prop="language" label="语言" width="100" />
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.status)">
              {{ statusLabel(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="总分" width="80">
          <template #default="{ row }">
            <span
              :style="{
                color:
                  (row.score?.totalScore ?? 0) >= 75
                    ? '#67c23a'
                    : (row.score?.totalScore ?? 0) >= 60
                      ? '#e6a23c'
                      : '#f56c6c',
              }"
            >
              {{ row.score?.totalScore?.toFixed(1) ?? '-' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="提交时间" width="160">
          <template #default="{ row }">
            {{ formatLocalTime(row.createdAt) }}
          </template>
        </el-table-column>
      </el-table>

      <el-pagination
        style="margin-top: 16px; justify-content: flex-end; display: flex;"
        :current-page="store.currentPage"
        :page-size="store.pageSize"
        :total="store.total"
        layout="total, prev, pager, next"
        @current-change="onPageChange"
      />
    </el-card>
  </div>
</template>

<style scoped>
:deep(.cursor-pointer) {
  cursor: pointer;
}
</style>
