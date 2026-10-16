[CmdletBinding()]
param([string]$Repository = 'puneeee/superstar-reminder')
$ErrorActionPreference = 'Stop'
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'Install the official GitHub CLI first: https://cli.github.com/ . Run its installer from your D: downloads folder.' }
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    gh auth status
    if ($LASTEXITCODE -ne 0) {
        gh auth login --hostname github.com --git-protocol https --web
        if ($LASTEXITCODE -ne 0) { throw 'GitHub authentication was not completed.' }
    }
    gh repo view $Repository --json name 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        gh repo create $Repository --public --source . --remote origin --description 'Animated desktop reminders for Windows and macOS, built with Avalonia and MySQL.'
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the public repository.' }
    }
    $remote = git remote get-url origin 2>$null
    if (-not $remote) { git remote add origin "https://github.com/$Repository.git" }
    elseif ($remote -ne "https://github.com/$Repository.git" -and $remote -ne "git@github.com:$Repository.git") { throw 'The existing origin points elsewhere. Review it before publishing.' }
    gh auth setup-git
    git push --set-upstream origin main
    if ($LASTEXITCODE -ne 0) { throw 'Push failed. Local commits have been retained.' }
    gh repo view $Repository --web
} finally { Pop-Location }
