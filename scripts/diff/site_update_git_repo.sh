#!/bin/sh
node site_update_git_repo.js
git -C repo/ remote add origin git@github.com:xSke/blaseball-site-files
git -C repo/ push --force origin main