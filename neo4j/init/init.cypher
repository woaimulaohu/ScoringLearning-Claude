// ──────────────────────────────────────
// CCTaskScoring Neo4j 初始化脚本
// 创建约束和索引，确保数据完整性和查询性能
// ──────────────────────────────────────

// ── 唯一性约束 ──

// Task 节点：taskId 唯一
CREATE CONSTRAINT task_id_unique IF NOT EXISTS
FOR (t:Task) REQUIRE t.taskId IS UNIQUE;

// Lesson 节点：lessonId 唯一
CREATE CONSTRAINT lesson_id_unique IF NOT EXISTS
FOR (l:Lesson) REQUIRE l.lessonId IS UNIQUE;

// ErrorPattern 节点：name 唯一
CREATE CONSTRAINT error_pattern_name_unique IF NOT EXISTS
FOR (e:ErrorPattern) REQUIRE e.name IS UNIQUE;

// Tag 节点：name 唯一
CREATE CONSTRAINT tag_name_unique IF NOT EXISTS
FOR (tag:Tag) REQUIRE tag.name IS UNIQUE;

// ── 索引（加速查询） ──

// Task 按语言查询
CREATE INDEX task_language_index IF NOT EXISTS
FOR (t:Task) ON (t.language);

// Task 按创建时间排序
CREATE INDEX task_created_at_index IF NOT EXISTS
FOR (t:Task) ON (t.createdAt);

// Lesson 按创建时间排序
CREATE INDEX lesson_created_at_index IF NOT EXISTS
FOR (l:Lesson) ON (l.createdAt);

// ErrorPattern 按频率排序（高频错误模式查询）
CREATE INDEX error_pattern_frequency_index IF NOT EXISTS
FOR (e:ErrorPattern) ON (e.frequency);

// ErrorPattern 按严重程度筛选
CREATE INDEX error_pattern_severity_index IF NOT EXISTS
FOR (e:ErrorPattern) ON (e.severity);
