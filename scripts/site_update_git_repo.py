import asyncio, aiohttp, pygit2, os, subprocess, sys, re, io
from datetime import datetime, timezone, timedelta

BASE_URL = "https://api.sibr.dev/chronicler/v1"

async def main(sess):
    repo = pygit2.init_repository(sys.argv[1], False, initial_head="main")
    head_timestamp = get_head_timestamp(repo)

    site_updates_req = await sess.get(BASE_URL + "/site/updates")
    site_updates = await site_updates_req.json()

    current_files = {}
    files_last_commit = {}

    # response is latest first, so reverse for chronological
    for (timestamp, updates) in group_updates_by_minute(site_updates["data"]):
        current_files = dict(**files_last_commit)
        for update in updates:
            filename = get_inner_filename(update["path"])
            current_files[filename] = update
        
        if not head_timestamp or timestamp > head_timestamp:
            await fetch_and_add_all(sess, repo, updates)
            make_commit(repo, timestamp, make_commit_message(files_last_commit, current_files))

        files_last_commit = current_files
    print("done! :) current HEAD at {} @ {}".format(repo.revparse_single("HEAD").hex, get_head_timestamp(repo)))


async def fetch_and_add_all(sess, repo, updates):
    async def inner(update):
        filename = get_filename(update["path"])
        print("fetching and processing: {}".format(filename))
        data = await fetch_and_process_file(sess, BASE_URL + update["downloadUrl"], filename)

        print("adding to repo: {} @ {}".format(filename, parse_timestamp(update["timestamp"])))
        add_update_to_repo(sess, repo, get_inner_filename(filename), data)

    await asyncio.gather(*[inner(update) for update in updates])

async def fetch_and_process_file(sess, url, filename):
    data_req = await sess.get(url)
    data = await data_req.read()
    data = await prettify_code(data, filename)
    return data

def get_head_timestamp(repo):
    if repo.head_is_unborn:
        return None

    head_commit = repo.revparse_single("HEAD")
    return datetime.fromtimestamp(head_commit.author.time, tz=timezone.utc)

def parse_timestamp(isostr):
    # Python doesn't like the Z suffix (or milliseconds) and we want a UTC-aware datetime
    return datetime.fromisoformat(isostr.split(".")[0].replace("Z", "")).replace(tzinfo=timezone.utc)

def get_minute(datetime):
    return datetime.replace(second=0, microsecond=0) + timedelta(minutes=1)

def group_updates_by_minute(updates):
    last_minute = None
    group = []
    for update in updates:
        this_minute = get_minute(parse_timestamp(update["timestamp"]))
        if last_minute and last_minute != this_minute:
            yield (last_minute, group)
            group = []
        group.append(update)
        last_minute = this_minute
    if group:
        yield (last_minute, group)

def add_update_to_repo(sess, repo, filename, data):
    blob_id = repo.create_blob(data)

    entry = pygit2.IndexEntry(filename, blob_id, pygit2.GIT_FILEMODE_BLOB)
    repo.index.add(entry)
    repo.index.write()
    
def make_commit(repo, timestamp, commit_message):
    tree_id = repo.index.write_tree()

    author = pygit2.Signature("The Game Band", "dontmailthis@example.com", time=int(timestamp.timestamp()))
    committer = pygit2.Signature("Chronicler", "hi@sibr.dev", time=int(timestamp.timestamp()))

    parent = repo.branches.get("main")
    repo.create_commit("refs/heads/main", author, committer, commit_message, tree_id, [parent.target] if parent else [])
    print("commit:", timestamp, commit_message.split("\n")[0])

    repo.reset(repo.head.target, pygit2.GIT_RESET_HARD)
    repo.checkout_index()


def make_commit_message(last_commit_files, current_files):
    segments = []
    changed_files = []
    for filename in current_files.keys():
        old_path = get_filename(last_commit_files[filename]["path"]) if filename in last_commit_files else None
        new_path = get_filename(current_files[filename]["path"])

        changed = True
        if not old_path:
            segments.append("{} (new)".format(new_path))
        elif old_path != new_path:
            segments.append("{} -> {}".format(old_path, new_path))
        elif last_commit_files[filename]["hash"] != current_files[filename]["hash"]:
            segments.append("{}".format(new_path))
        else:
            changed = False

        if changed:
            changed_files.append(filename)
    return "Site update: {}\n\n{}".format(", ".join(changed_files), "\n".join(segments))

def get_filename(path):
    if path == "/":
        return "index.html"
    return os.path.basename(path)
    
def get_inner_filename(path):
    segments = get_filename(path).split(".")
    return segments[0] + "." + segments[-1]

async def prettify_code(input, filename):
    input = strip_json_parse_literals(input)

    proc = await asyncio.create_subprocess_shell(
        "npx prettier --stdin-filepath {}".format(filename),
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE
    )
    stdout, _ = await proc.communicate(input=input)
    return stdout


def strip_json_parse_literals(input):
    text = list(input.decode("utf-8"))
    
    i = 0
    # This code is incredibly cursed but basically strips "JSON.parse()"s out into object literals so the formatter can get at it properly
    while i < len(text):
        if text[i:i+12] != list("JSON.parse('"):
            i += 1
            continue
    
        # Strip leading JSON.parse('
        text[i:i+12] = []

        # Strip escaped single-quotes inside string
        while True:
            if text[i] == "'":
                if text[i-1] == "\\":
                    text[i-1:i] = []
                else:
                    break
            i += 1
        
        # Strip trailing ')
        text[i:i+2] = []

    return "".join(text).encode("utf-8")


if __name__ == "__main__":
    async def inner():
        async with aiohttp.ClientSession() as sess:
            await main(sess)
    asyncio.get_event_loop().run_until_complete(inner())