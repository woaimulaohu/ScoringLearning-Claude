<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { tasksApi } from '@/api/tasks';
import type { ErrorPattern } from '@/types';

const summary = ref({ total: 0, needsReview: 0, averageScore: 0 });
const errorPatterns = ref<ErrorPattern[]>([]);
const loading = ref(false);

const fetchData = async () => {
  loading.value = true;
  try {
    const [summaryRes, patternsRes] = await Promise.all([
      tasksApi.getSummary(),
      tasksApi.getErrorPatterns(10),
    ]);
    summary.value = summaryRes.data;
    errorPatterns.value = patternsRes.data;
  } catch {
    // 降级处理
  } finally {
    loading.value = false;
  }
};

onMounted(fetchData);
</script>

<template>
  <div v-loading="loading">
    <!-- 统计卡片 -->
    <el-row :gutter="16" style="margin-bottom: 24px;">
      <el-col :span="8">
        <el-card shadow="hover">
          <div style="text-align: center;">
            <div style="font-size: 36px; font-weight: bold; color: #409EFF;">
              {{ summary.total }}
            </div>
            <div style="color: #909399; margin-top: 8px;">总任务数</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card shadow="hover">
          <div style="text-align: center;">
            <div style="font-size: 36px; font-weight: bold; color: #F56C6C;">
              {{ summary.needsReview }}
            </div>
            <div style="color: #909399; margin-top: 8px;">待审查任务</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card shadow="hover">
          <div style="text-align: center;">
            <div
              style="font-size: 36px; font-weight: bold;"
              :style="{
                color: summary.averageScore >= 75 ? '#67c23a' : '#e6a23c',
              }"
            >
              {{ summary.averageScore.toFixed(1) }}
            </div>
            <div style="color: #909399; margin-top: 8px;">平均评分</div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 高频错误模式 -->
    <el-card>
      <template #header>高频错误模式 Top 10</template>
      <el-empty v-if="errorPatterns.length === 0" description="暂无数据" />
      <div v-else>
        <div
          v-for="(item, index) in errorPatterns"
          :key="item.name"
          style="margin-bottom: 12px;"
        >
          <div
            style="display: flex; justify-content: space-between; margin-bottom: 4px;"
          >
            <span>{{ index + 1 }}. {{ item.name }}</span>
            <span style="color: #909399;">{{ item.frequency }} 次</span>
          </div>
          <el-progress
            :percentage="
              Math.min(
                (item.frequency / (errorPatterns[0]?.frequency || 1)) * 100,
                100
              )
            "
            :color="
              index < 3 ? '#F56C6C' : index < 6 ? '#E6A23C' : '#409EFF'
            "
            :show-text="false"
          />
        </div>
      </div>
    </el-card>
  </div>
</template>
