# ⚡ QUICK REFERENCE - Git Workflow Card

**Print this or keep it visible while executing**

---

## 🌳 Branch Structure

```
feature/song-structure (MAIN FEATURE BRANCH)
├─ feature/cqrs-projects (Stages 1-3: Setup)
├─ feature/cqrs-commands (Stage 4: Commands)
├─ feature/cqrs-queries (Stage 5: Queries)
├─ feature/cqrs-buses (Stage 6: Buses)
└─ feature/cqrs-viewmodels (Stage 7: Complete)
```

---

## 📍 Where You Are

### Before Starting
```bash
# Make sure you're on song-structure
git checkout feature/song-structure
git pull origin feature/song-structure
```

### Stage 1-3
```bash
# Create ONE branch for stages 1-3
git checkout -b feature/cqrs-projects

# After stages 1, 2, and 3, push once
git push origin feature/cqrs-projects
```

### Stages 4-7
```bash
# For each stage, create NEW branch from song-structure
git checkout feature/song-structure
git pull origin feature/song-structure
git checkout -b feature/cqrs-{stage-name}

# After stage, push and create PR
git push origin feature/cqrs-{stage-name}
# Create PR on GitHub

# After PR merged, pull back
git checkout feature/song-structure
git pull origin feature/song-structure
```

---

## 📝 Commit Template

```bash
# Stage 1-3 (each commit as you go)
git add .
git commit -m "feat(cqrs): [DESCRIPTION]"
git push origin feature/cqrs-projects

# Stage 4+
git add .
git commit -m "feat(cqrs): [DESCRIPTION]"
git push origin feature/cqrs-{stage-name}
# Then create PR on GitHub
```

---

## 🔄 Commit Messages by Stage

### Stage 1 (Projects)
```
feat(cqrs): create application, domain, infrastructure, and test projects
```

### Stage 2 (Interfaces)
```
feat(cqrs): define domain service interfaces
```

### Stage 3 (Infrastructure)
```
feat(cqrs): implement infrastructure service wrappers
```

### Stage 4 (Commands)
```
feat(cqrs): implement commands and handlers
```

### Stage 5 (Queries)
```
feat(cqrs): implement queries and handlers
```

### Stage 6 (Buses)
```
feat(cqrs): implement command and query buses
```

### Stage 7 (ViewModels)
```
feat(cqrs): implement viewmodels and dependency injection
```

---

## 🔀 Between Stages

### After Stage 3, before Stage 4
```bash
# Final push for cqrs-projects
git push origin feature/cqrs-projects

# Create PR on GitHub (feature/cqrs-projects → feature/song-structure)
# After merge, pull:
git checkout feature/song-structure
git pull origin feature/song-structure

# Now start Stage 4 with new branch
git checkout -b feature/cqrs-commands
```

### After Each Stage 4-7
```bash
# 1. Push
git push origin feature/cqrs-{name}

# 2. Go to GitHub, create PR to feature/song-structure

# 3. After merge, pull back
git checkout feature/song-structure
git pull origin feature/song-structure

# 4. Create next branch (or finish if Stage 7)
```

---

## ✅ Verification Commands

```bash
# Check which branch you're on
git branch -v

# See commits since last pull
git log --oneline -5

# Check uncommitted changes
git status

# See differences
git diff

# Undo last commit (if needed)
git reset --soft HEAD~1
```

---

## 🚨 If You Get Lost

```bash
# Check current status
git status

# Show what branch you're on
git branch -v

# Show last few commits
git log --oneline -10

# If you committed but haven't pushed
git push origin {branch-name}

# If you're on wrong branch
git checkout feature/song-structure
git pull origin feature/song-structure
```

---

## 📊 Files Created per Stage

### Stage 1-3
- 4 × .csproj files
- 1 × solution file update
- 5 × service interfaces
- 3 × service wrappers

### Stage 4
- 5 × command classes
- 1 × handler interface
- 5 × handler implementations

### Stage 5
- 3 × query classes
- 1 × handler interface
- 3 × handler implementations
- 3 × DTOs

### Stage 6
- 1 × ICommandBus interface
- 1 × CommandBus implementation
- 1 × IQueryBus interface
- 1 × QueryBus implementation

### Stage 7
- 1 × ViewModelBase
- 2 × ViewModels
- 1 × Updated App.xaml.cs
- 1 × Updated MainWindow.xaml.cs
- 1 × Updated MainWindow.xaml

---

## 🎯 Success Criteria per Stage

### After Stage 3
```bash
dotnet build  ✅ Builds successfully
```

### After Stage 4
```bash
dotnet build  ✅ Builds successfully
# No runtime yet (need buses)
```

### After Stage 5
```bash
dotnet build  ✅ Builds successfully
# No runtime yet (need ViewModels)
```

### After Stage 6
```bash
dotnet build  ✅ Builds successfully
# No runtime yet (need DI wired)
```

### After Stage 7
```bash
dotnet build  ✅ Builds successfully
dotnet run    ✅ App boots and runs
# Buttons work, library shows, flow state achieved 🚀
```

---

## 📚 Keep These Handy

- Terminal/PowerShell window (for git commands)
- `RAPID_EXECUTION_PLAYBOOK.md` (the main guide)
- Text editor with your code (VS Code or similar)
- GitHub (for creating PRs)

---

**Print this page. Keep it visible. Reference frequently.**

**You've got this.** ⚡
