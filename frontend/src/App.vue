<template>
  <el-container style="height: 100vh;">
    <el-aside width="200px" style="background: #304156;">
      <div class="logo">ClaudeCode 评分</div>
      <el-menu
        router
        :default-active="$route.path"
        background-color="#304156"
        text-color="#bfcbd9"
        active-text-color="#409EFF"
      >
        <el-menu-item index="/dashboard">
          <el-icon><DataAnalysis /></el-icon>
          <span>仪表板</span>
        </el-menu-item>
        <el-menu-item index="/tasks">
          <el-icon><List /></el-icon>
          <span>任务列表</span>
        </el-menu-item>
      </el-menu>
    </el-aside>
    <el-container>
      <el-header
        style="display: flex; align-items: center; background: #fff; border-bottom: 1px solid #eee;"
      >
        <span style="font-size: 16px; font-weight: 500;">{{ $route.meta.title }}</span>
      </el-header>
      <el-main style="background: #f5f7fa;">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

.logo {
  color: #fff;
  font-size: 16px;
  font-weight: bold;
  padding: 20px;
  text-align: center;
  border-bottom: 1px solid #3a4b5c;
}
</style>
