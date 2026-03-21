<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue';
import type { Lesson } from '@/types';
import { tasksApi } from '@/api/tasks';
import { ElMessage } from 'element-plus';
import { formatLocalDate } from '@/utils/datetime';

const props = defineProps<{
  taskId: string;
}>();

const lessons = ref<Lesson[]>([]);
const loading = ref(false);
const showAddForm = ref(false);
const newLesson = reactive({ problem: '', cause: '', suggestion: '' });

const fetchLessons = async () => {
  loading.value = true;
  try {
    const res = await tasksApi.getLessons(props.taskId);
    lessons.value = res.data;
  } catch {
    // 忽略错误（API 可能未实现）
  } finally {
    loading.value = false;
  }
};

const addLesson = async () => {
  try {
    await tasksApi.addLesson(props.taskId, { ...newLesson });
    ElMessage.success('教训已添加');
    showAddForm.value = false;
    Object.assign(newLesson, { problem: '', cause: '', suggestion: '' });
    fetchLessons();
  } catch {
    ElMessage.error('添加失败');
  }
};

onMounted(fetchLessons);
</script>

<template>
  <el-card>
    <template #header>
      <div style="display: flex; justify-content: space-between; align-items: center;">
        <span>相关经验教训</span>
        <el-button size="small" @click="showAddForm = !showAddForm">
          <el-icon><Plus /></el-icon>
          添加教训
        </el-button>
      </div>
    </template>

    <el-collapse-transition>
      <div v-if="showAddForm" style="margin-bottom: 16px;">
        <el-form :model="newLesson" label-width="80px">
          <el-form-item label="问题">
            <el-input v-model="newLesson.problem" placeholder="描述问题" />
          </el-form-item>
          <el-form-item label="原因">
            <el-input v-model="newLesson.cause" placeholder="分析原因" />
          </el-form-item>
          <el-form-item label="建议">
            <el-input
              v-model="newLesson.suggestion"
              type="textarea"
              placeholder="改进建议"
            />
          </el-form-item>
          <el-form-item>
            <el-button type="primary" size="small" @click="addLesson">
              保存
            </el-button>
            <el-button size="small" @click="showAddForm = false">
              取消
            </el-button>
          </el-form-item>
        </el-form>
      </div>
    </el-collapse-transition>

    <div v-loading="loading">
      <el-empty
        v-if="!loading && lessons.length === 0"
        description="暂无相关教训"
        :image-size="80"
      />
      <el-timeline v-else>
        <el-timeline-item
          v-for="lesson in lessons"
          :key="lesson.id"
          :timestamp="formatLocalDate(lesson.createdAt)"
        >
          <el-card shadow="hover">
            <p><strong>问题：</strong>{{ lesson.problem }}</p>
            <p style="color: #909399; margin-top: 4px;">
              <strong>原因：</strong>{{ lesson.cause }}
            </p>
            <p style="color: #409eff; margin-top: 4px;">
              <strong>建议：</strong>{{ lesson.suggestion }}
            </p>
          </el-card>
        </el-timeline-item>
      </el-timeline>
    </div>
  </el-card>
</template>
