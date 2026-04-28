# Linux Shell Essentials for Windows Users

A focused guide for Windows developers who need to become comfortable in a Linux shell quickly — specifically for CKAD-style work (WSL2 + Ubuntu + `kubectl` + `vim`).

The goal is **not** to learn Linux. The goal is to stop hesitating in a Linux terminal under time pressure.

---

## Table of Contents

- [1. Mental Model: What Changes From Windows](#1-mental-model-what-changes-from-windows)
- [2. Getting Into a Linux Shell on Windows](#2-getting-into-a-linux-shell-on-windows)
- [3. Filesystem Basics](#3-filesystem-basics)
- [4. Navigating and Inspecting Files](#4-navigating-and-inspecting-files)
- [5. Creating, Copying, Moving, Deleting](#5-creating-copying-moving-deleting)
- [6. Redirection and Pipes (`>`, `>>`, `|`)](#6-redirection-and-pipes---)
- [7. Searching and Filtering Text](#7-searching-and-filtering-text)
- [8. Permissions and `sudo`](#8-permissions-and-sudo)
- [9. Environment Variables and Aliases](#9-environment-variables-and-aliases)
- [10. Processes, Jobs, and Signals](#10-processes-jobs-and-signals)
- [11. PowerShell → Bash Cheat Sheet](#11-powershell--bash-cheat-sheet)
- [12. Common Pitfalls for Windows Users](#12-common-pitfalls-for-windows-users)
- [13. 15-Minute Warm-Up Drill](#13-15-minute-warm-up-drill)

---

## 1. Mental Model: What Changes From Windows

| Thing | Windows | Linux |
|---|---|---|
| Path separator | `\` | `/` |
| Drive letters | `C:\...` | No drives. One root: `/` |
| Case sensitivity | No (`File.txt` = `file.txt`) | **Yes** (`File.txt` ≠ `file.txt`) |
| Line endings | CRLF (`\r\n`) | LF (`\n`) |
| Executable marker | `.exe` extension | File has **execute bit** (`chmod +x`) |
| Home directory | `C:\Users\you` | `/home/you` = `~` |
| Config files | Registry + various | Plain text, usually in `~` or `/etc` |
| Shell | PowerShell / cmd | bash (default in WSL Ubuntu) |

Keep these in mind:

- **Everything is a file** — devices, sockets, pipes, even process info under `/proc`.
- **Hidden files** start with a dot: `.bashrc`, `.gitignore`. Use `ls -a` to see them.
- **Commands are programs**, not verbs. `ls` is `/usr/bin/ls`, found via `$PATH`.

---

## 2. Getting Into a Linux Shell on Windows

### 2.1 Install WSL2 + Ubuntu (once)

In PowerShell **as Administrator**:

```powershell
wsl --install -d Ubuntu
```

Reboot if prompted. On first launch of Ubuntu, create a Linux username + password (does not need to match Windows).

### 2.2 Daily use

- Start menu → **Ubuntu** (or type `wsl` in any terminal)
- Or from Windows Terminal: open a new **Ubuntu** tab.

### 2.3 Where are my files?

- Linux home: `/home/<you>` (= `~`)
- Windows drives are mounted under `/mnt`: your `C:\` is at `/mnt/c/`.
- **Work inside `~`**, not under `/mnt/c`. WSL filesystem I/O is far faster on the Linux side, and tools like `vim` and `git` behave better there.

### 2.4 Open VS Code from WSL

```bash
cd ~/projects/myrepo
code .
```

This launches VS Code on Windows, connected to the WSL filesystem (Remote‑WSL).

---

## 3. Filesystem Basics

```
/            root of everything
├── home/
│   └── you/        ← your home, aka ~
├── etc/            ← system config (read-only-ish)
├── var/            ← logs, variable data
├── tmp/            ← scratch space, wiped on reboot
├── usr/bin/        ← most commands live here
└── mnt/c/          ← your Windows C: drive (WSL only)
```

Key shortcuts:

- `~` → your home directory
- `.` → current directory
- `..` → parent directory
- `-` (as argument to `cd`) → previous directory

---

## 4. Navigating and Inspecting Files

| Command | What it does |
|---|---|
| `pwd` | Print working directory |
| `ls` | List files in current dir |
| `ls -la` | Long listing, including hidden |
| `cd <dir>` | Change directory |
| `cd` | Go home (`~`) |
| `cd -` | Go back to previous dir |
| `tree -L 2` | Show 2-level tree (may need `sudo apt install tree`) |
| `cat file` | Print file contents |
| `less file` | Page through file (`q` to quit, `/text` to search) |
| `head -n 20 file` | First 20 lines |
| `tail -n 20 file` | Last 20 lines |
| `tail -f file` | Follow a file as it grows (logs) |
| `wc -l file` | Count lines |
| `file thing` | Guess what kind of file it is |

Examples:

```bash
ls -la ~/kube
cat ~/.bashrc | less
tail -f /var/log/syslog
```

---

## 5. Creating, Copying, Moving, Deleting

| Command | Purpose |
|---|---|
| `mkdir name` | Make directory |
| `mkdir -p a/b/c` | Make nested path, no error if exists |
| `touch file` | Create empty file (or update timestamp) |
| `cp src dst` | Copy file |
| `cp -r src dst` | Copy directory recursively |
| `mv src dst` | Move **or rename** (same command) |
| `rm file` | Delete file |
| `rm -r dir` | Delete directory recursively |
| `rm -rf dir` | Force recursive delete (**dangerous**, no undo, no recycle bin) |

> Linux has **no recycle bin**. `rm` is permanent. Double-check the path, especially with `-rf`.

---

## 6. Redirection and Pipes (`>`, `>>`, `|`)

This is the single biggest productivity leap coming from PowerShell.

| Operator | Meaning |
|---|---|
| `cmd > file` | Send stdout to `file` (overwrite) |
| `cmd >> file` | Append stdout to `file` |
| `cmd 2> file` | Send stderr to `file` |
| `cmd > out 2>&1` | Send stdout + stderr to `out` |
| `cmd < file` | Feed `file` as stdin |
| `cmd1 \| cmd2` | Pipe stdout of `cmd1` into stdin of `cmd2` |

Examples, CKAD-flavored:

```bash
# Save a manifest generated from kubectl
kubectl get pod mypod -o yaml > pod.yaml

# Append a line to ~/.bashrc
echo 'alias k=kubectl' >> ~/.bashrc

# Find failing pods across the cluster
kubectl get pods -A | grep -E 'CrashLoopBackOff|Error'

# Count them
kubectl get pods -A | grep CrashLoopBackOff | wc -l
```

Heredoc (multi-line text into a command or file):

```bash
cat > pod.yaml <<'EOF'
apiVersion: v1
kind: Pod
metadata:
  name: demo
spec:
  containers:
    - name: app
      image: nginx
EOF
```

Use `<<'EOF'` (quoted) to write `$vars` **literally** instead of expanding them.

---

## 7. Searching and Filtering Text

| Tool | Typical use |
|---|---|
| `grep pattern file` | Find lines matching `pattern` |
| `grep -r pattern dir` | Recurse into a directory |
| `grep -E 'a\|b'` | Extended regex (alternation) |
| `grep -v pattern` | **Invert** — lines NOT matching |
| `grep -i pattern` | Case-insensitive |
| `sort` | Sort lines |
| `uniq` | Collapse adjacent duplicate lines (usually after `sort`) |
| `wc -l` | Count lines |
| `cut -d: -f1` | Split each line on `:` and take field 1 |
| `awk '{print $2}'` | Print 2nd whitespace-separated field |
| `sed 's/foo/bar/g'` | Stream-edit: replace `foo` with `bar` |
| `xargs` | Turn stdin into args for another command |

Examples:

```bash
# Pods whose name contains "web"
kubectl get pods -A | grep web

# Unique images used by pods (approximation)
kubectl get pods -A -o jsonpath='{.items[*].spec.containers[*].image}' \
  | tr ' ' '\n' | sort -u

# Delete every pod matching "test-" (dangerous, read twice)
kubectl get pods -o name | grep '^pod/test-' | xargs -r kubectl delete
```

For CKAD, `grep` + `wc -l` + occasionally `awk '{print $1}'` covers ~95% of what you need.

---

## 8. Permissions and `sudo`

Each file has an owner, a group, and three permission triplets — user / group / other — each with read (`r`), write (`w`), execute (`x`).

```bash
ls -l script.sh
# -rwxr-xr-x 1 you you  120 Apr 22 10:00 script.sh
```

Change permissions:

```bash
chmod +x script.sh       # make executable
chmod 644 file           # rw-r--r--
chmod 755 script.sh      # rwxr-xr-x
```

Change ownership (usually needs `sudo`):

```bash
sudo chown you:you file
```

**`sudo`** runs a single command as root. Use it sparingly:

```bash
sudo apt update
sudo apt install -y bash-completion
```

If you find yourself typing `sudo` in your own home directory, you are almost certainly doing something wrong.

---

## 9. Environment Variables and Aliases

```bash
# Read
echo $HOME
echo $PATH

# Set (current shell only)
export EDITOR=vim

# Use in a command
kubectl get pods -n $NAMESPACE

# Persist: add to ~/.bashrc
echo 'export EDITOR=vim' >> ~/.bashrc
source ~/.bashrc
```

Aliases are shell shortcuts:

```bash
alias k=kubectl
alias ll='ls -la'
```

To persist, put them in `~/.bashrc` (see [CKAD/README.md §2.2](README.md#22-permanent-setup-on-wsl--ubuntu-practice-machine)).

Key files:

| File | Runs when |
|---|---|
| `~/.bashrc` | Every interactive non-login shell (the default in WSL) |
| `~/.profile` | Login shells; on Ubuntu it sources `~/.bashrc` |
| `~/.bash_profile` | Login shells (if present). Default Ubuntu does **not** use this — do not put CKAD setup here |

---

## 10. Processes, Jobs, and Signals

```bash
# What's running
ps -ef | grep kubectl
top                    # live view, q to quit
htop                   # nicer version, `sudo apt install htop`

# Background a command: append &
kubectl port-forward svc/web 8080:80 &

# See/kill background jobs of the current shell
jobs
kill %1                # kill background job 1
kill <PID>             # by PID
kill -9 <PID>          # SIGKILL (last resort)

# Foreground interruption: Ctrl+C
# Suspend a foreground job: Ctrl+Z, then `bg` to resume in background
```

---

## 11. PowerShell → Bash Cheat Sheet

| Goal | PowerShell | Bash |
|---|---|---|
| List dir | `Get-ChildItem` / `ls` | `ls` / `ls -la` |
| Current dir | `Get-Location` / `pwd` | `pwd` |
| Change dir | `Set-Location` / `cd` | `cd` |
| Show file | `Get-Content file` | `cat file` / `less file` |
| First N lines | `Get-Content file -First 20` | `head -n 20 file` |
| Last N lines, follow | `Get-Content file -Tail 20 -Wait` | `tail -n 20 -f file` |
| Copy | `Copy-Item src dst` | `cp src dst` |
| Move / rename | `Move-Item / Rename-Item` | `mv src dst` |
| Delete | `Remove-Item -Recurse -Force dir` | `rm -rf dir` |
| Grep | `Select-String pattern file` | `grep pattern file` |
| Env var | `$env:NAME = "x"` | `export NAME=x` |
| Which command | `Get-Command foo` | `which foo` / `type foo` |
| Help | `Get-Help cmd` | `man cmd` / `cmd --help` |
| Pipe | `\|` (objects) | `\|` (text streams) |

Important difference: **PowerShell pipes objects, bash pipes text.** Everything between `|` in bash is plain bytes/lines, which is why `grep`, `awk`, `cut` exist.

---

## 12. Common Pitfalls for Windows Users

- **Case sensitivity.** `Pod.yaml` and `pod.yaml` are two different files. Tab-completion is your friend — use it.
- **CRLF line endings.** Files edited in Notepad often break on Linux. Configure git: `git config --global core.autocrlf input`. In `vim`, fix with `:set fileformat=unix` then `:wq`.
- **Working under `/mnt/c`.** Slow and permission-quirky. Keep CKAD practice in `~`, not in your Windows user folder.
- **Copy/paste in terminals.** In Windows Terminal: `Ctrl+Shift+C` / `Ctrl+Shift+V`. Never paste with middle-click inside tmux sessions you don't own.
- **`rm -rf` has no undo.** Always check `pwd` first. Never expand a shell variable into `rm -rf "$VAR/"` without confirming `$VAR` is set.
- **Quotes matter.**
  - `"..."` — expands `$vars` and `$(...)`.
  - `'...'` — literal, no expansion.
  - Backticks / `$(...)` — command substitution.
- **`sudo` is not a magic fix.** If `kubectl` fails, the answer is almost never `sudo kubectl`. It's usually the kubeconfig or namespace.
- **Running `.bashrc` changes.** Edits to `~/.bashrc` take effect only after `source ~/.bashrc` or a new shell.

---

## 13. 15-Minute Warm-Up Drill

Run this inside WSL. If anything takes more than a few seconds, repeat until it doesn't.

```bash
# 1. Navigation
cd ~
pwd
mkdir -p ckad-drill/sub && cd ckad-drill/sub
cd -          # back to home
cd ckad-drill

# 2. Files
echo "hello" > greeting.txt
cat greeting.txt
cp greeting.txt g2.txt
mv g2.txt renamed.txt
ls -la
rm renamed.txt

# 3. Redirection + pipes
printf "apple\nbanana\napple\ncherry\n" > fruits.txt
cat fruits.txt | sort | uniq -c
grep -v apple fruits.txt

# 4. Env + alias
export NS=practice
echo "using $NS"
alias ll='ls -la'
ll

# 5. vim round-trip (the real test)
vim notes.md
# inside vim: i, type 3 lines, Esc, :wq

# 6. Cleanup
cd ~
rm -rf ckad-drill
```

If you completed that without Googling, you have enough Linux for CKAD. Now go drill `kubectl` and `vim`.

---

## Related

- [CKAD/README.md](README.md) — main CKAD playbook (environment, speed patterns, exam runbook).
- [CKAD/drills-1-core.md](drills-1-core.md) — 25 timed CKAD drills with hidden answers.
