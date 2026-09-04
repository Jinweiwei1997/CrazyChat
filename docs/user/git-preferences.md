> 本文件记录本项目跨会话生效的 Git 工作流偏好。

# Git Preferences

- 基线：使用 `main`，禁止任何删除基线分支的操作。
- Branch/Worktree：始终沿用当前分支，不为任务创建 feature branch 或 worktree。
- Sync：prepare 时先刷新并同步远端最新状态；提交后、push 前再次刷新并 rebase 到远端最新状态；明确冲突自动解决，存在语义歧义时询问用户。
- Commit：checkpoint 或 finalize 时，当前工作单元完成且验证通过后自动 commit；未完成或验证失败时不提交。
- Merge：不创建 PR；不自动合入其他分支。工作在当前分支上完成（通常为 `main`）。
- Push：finalize 时 push 当前分支并核对实际远端 SHA。
- Cleanup：不自动删除分支或 worktree；永不删除基线 `main`。
- 最后更新：2026-09-04。
