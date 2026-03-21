<script setup lang="ts">
import { onMounted, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useTaskStore } from '@/stores/taskStore';
import ScorePanel from '@/components/ScorePanel.vue';
import LessonsPanel from '@/components/LessonsPanel.vue';
import type { ReviewRequest } from '@/types';
import { ElMessage } from 'element-plus';
import { formatLocalTime } from '@/utils/datetime';

const route = useRoute();
const router = useRouter();
const store = useTaskStore();
const taskId = route.params.id as string;

/** 解析 artifacts：后端可能返回 JSON 字符串或已解析的对象 */
const artifactsParsed = computed(() => {
  if (!store.currentTask?.artifacts) return null;
  if (typeof store.currentTask.artifacts === 'string') {
    try {
      return JSON.parse(store.currentTask.artifacts);
    } catch {
      return null;
    }
  }
  return store.currentTask.artifacts;
});

const onReview = async (req: ReviewRequest) => {
  try {
    await store.reviewTask(taskId, req);
    ElMessage.success('评分修正已提交');
  } catch {
    ElMessage.error('提交失败');
  }
};

onMounted(() => store.fetchTask(taskId));
</script>

<template>
  <div v-loading="store.loading">
    <el-button @click="router.back()" style="margin-bottom: 16px;">
      <el-icon><ArrowLeft /></el-icon>
      返回列表
    </el-button>

    <template v-if="store.currentTask">
      <el-row :gutter="16">
        <el-col :span="14">
          <!-- 任务信息 -->
          <el-card style="margin-bottom: 16px;">
            <template #header>任务信息</template>
            <el-descriptions :column="2" border>
              <el-descriptions-item label="任务ID">
                {{ store.currentTask.id }}
              </el-descriptions-item>
              <el-descriptions-item label="语言">
                {{ store.currentTask.language || '-' }}
              </el-descriptions-item>
              <el-descriptions-item label="状态">
                <el-tag>{{ store.currentTask.status }}</el-tag>
              </el-descriptions-item>
              <el-descriptions-item label="提交时间">
                {{ formatLocalTime(store.currentTask.createdAt) }}
              </el-descriptions-item>
              <el-descriptions-item label="描述" :span="2">
                {{ store.currentTask.description }}
              </el-descriptions-item>
            </el-descriptions>
          </el-card>

          <!-- 代码产出物 -->
          <el-card
            v-if="artifactsParsed?.code"
            style="margin-bottom: 16px;"
          >
            <template #header>
              <div style="display: flex; justify-content: space-between; align-items: center;">
                <span>代码产出物</span>
                <el-tag
                  v-if="store.currentTask.language"
                  size="small"
                  type="info"
                  effect="plain"
                >
                  {{ store.currentTask.language }}
                </el-tag>
              </div>
            </template>
            <pre
              style="
                background: #f5f7fa;
                padding: 16px;
                border-radius: 4px;
                overflow: auto;
                max-height: 400px;
                font-family: monospace;
                font-size: 13px;
                line-height: 1.5;
                tab-size: 2;
              "
            ><code>{{ artifactsParsed.code }}</code></pre>
          </el-card>

          <!-- 执行日志 -->
          <el-card v-if="store.currentTask.logs">
            <template #header>执行日志</template>
            <pre
              style="
                background: #1e1e1e;
                color: #d4d4d4;
                padding: 16px;
                border-radius: 4px;
                overflow: auto;
                max-height: 300px;
                font-family: monospace;
                font-size: 12px;
              "
            >{{ store.currentTask.logs }}</pre>
          </el-card>
        </el-col>

        <el-col :span="10">
          <!-- 评分面板 -->
          <div style="margin-bottom: 16px;">
            <ScorePanel
              :score="store.currentTask.score"
              :editable="
                store.currentTask.status === 'needs_review' ||
                store.currentTask.status === 'scored'
              "
              @submit="onReview"
            />
          </div>

          <!-- 经验教训 -->
          <LessonsPanel :task-id="taskId" />
        </el-col>
      </el-row>
    </template>
  </div>
</template>
