<script setup lang="ts">
import { computed, reactive } from 'vue';
import type { Score, ReviewRequest } from '@/types';

const props = defineProps<{
  score: Score | undefined;
  editable?: boolean;
}>();

const emit = defineEmits<{
  (e: 'submit', req: ReviewRequest): void;
}>();

const form = reactive<ReviewRequest>({
  completionScore: props.score?.completionScore,
  correctnessScore: props.score?.correctnessScore,
  qualityScore: props.score?.qualityScore,
  efficiencyScore: props.score?.efficiencyScore,
  uxScore: props.score?.uxScore,
  reviewerComments: props.score?.reviewerComments,
});

const scoreColor = (val: number) =>
  val >= 75 ? '#67c23a' : val >= 60 ? '#e6a23c' : '#f56c6c';

const dimensions = computed(() => [
  {
    label: '任务完成度 (30%)',
    key: 'completionScore' as keyof ReviewRequest,
    value: props.score?.completionScore ?? 0,
  },
  {
    label: '代码正确性 (30%)',
    key: 'correctnessScore' as keyof ReviewRequest,
    value: props.score?.correctnessScore ?? 0,
  },
  {
    label: '代码质量 (20%)',
    key: 'qualityScore' as keyof ReviewRequest,
    value: props.score?.qualityScore ?? 0,
  },
  {
    label: '效率 (10%)',
    key: 'efficiencyScore' as keyof ReviewRequest,
    value: props.score?.efficiencyScore ?? 0,
  },
  {
    label: '用户体验 (10%)',
    key: 'uxScore' as keyof ReviewRequest,
    value: props.score?.uxScore ?? 0,
  },
]);

const onSubmit = () => emit('submit', { ...form });
</script>

<template>
  <el-card>
    <template #header>
      <div style="display: flex; justify-content: space-between; align-items: center;">
        <span>评分详情</span>
        <el-tag v-if="score && !score.autoScored" type="warning">已人工审查</el-tag>
        <el-tag v-else-if="score" type="info">自动评分</el-tag>
      </div>
    </template>

    <!-- 总分显示 -->
    <div v-if="score" style="text-align: center; margin-bottom: 20px;">
      <div
        style="font-size: 48px; font-weight: bold;"
        :style="{ color: scoreColor(score.totalScore) }"
      >
        {{ score.totalScore.toFixed(1) }}
      </div>
      <div style="color: #909399;">总分（满分 100）</div>
    </div>
    <div v-else style="text-align: center; color: #909399; margin-bottom: 20px;">
      暂无评分
    </div>

    <!-- 各维度进度条 -->
    <div v-for="dim in dimensions" :key="dim.key" style="margin-bottom: 12px;">
      <div style="display: flex; justify-content: space-between; margin-bottom: 4px;">
        <span>{{ dim.label }}</span>
        <span :style="{ color: scoreColor(dim.value) }">{{ dim.value.toFixed(1) }}</span>
      </div>
      <el-progress
        :percentage="dim.value"
        :color="scoreColor(dim.value)"
        :stroke-width="10"
        :show-text="false"
      />
    </div>

    <!-- 审查员评语 -->
    <div v-if="score?.reviewerComments" style="margin-top: 16px;">
      <el-divider>审查员评语</el-divider>
      <p style="color: #606266;">{{ score.reviewerComments }}</p>
    </div>

    <!-- 编辑模式 -->
    <template v-if="editable">
      <el-divider>修正评分</el-divider>
      <el-form :model="form" label-width="140px">
        <el-form-item v-for="dim in dimensions" :key="dim.key" :label="dim.label">
          <el-input-number
            v-model="(form[dim.key] as number)"
            :min="0"
            :max="100"
            :precision="1"
            style="width: 160px;"
          />
        </el-form-item>
        <el-form-item label="审查员评语">
          <el-input v-model="form.reviewerComments" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="onSubmit">提交修正</el-button>
        </el-form-item>
      </el-form>
    </template>
  </el-card>
</template>
