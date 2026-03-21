export interface Task {
  id: string;
  description: string;
  language?: string;
  framework?: string;
  createdAt: string;
  completedAt?: string;
  artifacts?: { code?: string; tests?: string } | string;
  logs?: string;
  testResults?: { passed: number; failed: number; skipped: number };
  staticAnalysis?: { lintScore: number; issues: unknown[] };
  metadata?: { language: string; durationSec: number; attempts: number };
  status: 'pending' | 'scored' | 'needs_review' | 'reviewed';
  score?: Score;
}

export interface Score {
  id: number;
  taskId: string;
  completionScore: number;
  correctnessScore: number;
  qualityScore: number;
  efficiencyScore: number;
  uxScore: number;
  totalScore: number;
  autoScored: boolean;
  reviewerComments?: string;
  createdAt: string;
}

export interface Lesson {
  id: string;
  problem: string;
  cause: string;
  suggestion: string;
  createdAt: string;
  tags?: string[];
}

export interface ErrorPattern {
  name: string;
  description?: string;
  frequency: number;
  severity?: string;
}

export interface PagedResult<T> {
  data: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ReviewRequest {
  completionScore?: number;
  correctnessScore?: number;
  qualityScore?: number;
  efficiencyScore?: number;
  uxScore?: number;
  reviewerComments?: string;
}
