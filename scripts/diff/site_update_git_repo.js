const git = require("isomorphic-git");
const fs = require("fs");
const path = require("path");
const fetch = require("node-fetch").default;
const prettier = require("prettier");
const extractData = require("./extract_data.js");
const dataFormatter = require("./data_formatter.js");

const BASE_URL = "https://api.sibr.dev/chronicler/v1";

function getBaseFilename(path) {
    if (path === "/") return "index.html";
    const filename = path.split("/").pop();
    const segs = filename.split(".");
    return segs[0] + "." + segs[segs.length - 1];
}

async function getSiteUpdates() {
    const updates = await fetch(BASE_URL + "/site/updates").then((r) => r.json());
    return updates.data;
}

async function fetchUpdateFile(update) {
    const data = await fetch(BASE_URL + update.downloadUrl);
    return await data.text();
}

async function addFileGit(dir, filename, data) {
    const fullPath = path.join(dir, filename);
    await fs.promises.mkdir(path.dirname(fullPath), { recursive: true });
    await fs.promises.writeFile(path.join(dir, filename), data, {
        encoding: "utf8",
    });
    await git.add({ fs, dir, filepath: filename });
}

async function addExtraData(dir, extraData) {
    await addFileGit(
        dir,
        "data/weather.json",
        JSON.stringify(extraData.weather, null, 4)
    );

    await addFileGit(dir, "data/thebook.md", extraData.book);

    await addFileGit(
        dir,
        "data/items.json",
        JSON.stringify(extraData.items, null, 4)
    );

    await addFileGit(
        dir,
        "data/attributes.json",
        JSON.stringify(extraData.attributes, null, 4)
    );

    // Do we want this?
    /*await addFileGit(
        dir,
        "data/attributes.md",
        dataFormatter.formatAttributes(extraData.attributes)
    );*/
    
    // Only add this if we have a glossary
    if (extraData.glossary) {
        await addFileGit(
            dir,
            "data/glossary.json",
            JSON.stringify(extraData.glossary, null, 4)
        );

        await addFileGit(
            dir,
            "data/glossary.md",
            dataFormatter.formatGlossary(extraData.glossary)
        );
    }

    // Only add this if we have the library
    if (extraData.library) {
        await addFileGit(
            dir,
            "data/library.json",
            JSON.stringify(extraData.library, null, 4)
        );
    }
}

async function main(dir) {
    await git.init({ fs, dir, defaultBranch: "main" });

    let lastTimestamp = 0;
    try {
        const lastSha = await git.resolveRef({ fs, dir, ref: "main" });
        const lastCommit = await git.readCommit({ fs, dir, oid: lastSha });
        lastTimestamp = lastCommit.commit.author.timestamp;
    } catch (e) {}

    var updates = await getSiteUpdates();
    const groups = [];
    for (const update of updates) {
        const timestamp = Math.floor(+new Date(update.timestamp) / 1000);
        const minute = Math.floor(timestamp / 60) * 60;
        if (minute <= lastTimestamp) continue;
        if (!groups.length || groups[groups.length - 1].minute < minute)
            groups.push({ minute, updates: [] });
        groups[groups.length - 1].updates.push(update);
    }

    for (const group of groups) {
        for (const update of group.updates) {
            const filename = getBaseFilename(update.path);
            const data = await fetchUpdateFile(update);
            const { text, data: extraData } = prettifyAndExtract(
                data,
                filename,
                filename === "main.js"
            );

            await addFileGit(dir, filename, text);
            if (filename === "main.js" && extraData)
                await addExtraData(dir, extraData);
        }

        const timestamp = group.minute;
        await git.commit({
            fs,
            dir,
            author: {
                name: "The Game Band",
                email: "dontmailthis@example.com",
                timestamp,
                timezoneOffset: 0,
            },
            committer: {
                name: "Chronicler",
                email: "hi@sibr.dev",
                timestamp,
                timezoneOffset: 0,
            },
            message: "Site update @ " + new Date(group.minute * 1000).toISOString(),
        });

        console.dir(group.updates);
    }
}

function prettifyAndExtract(text, filename, extract) {
    if (filename.endsWith(".js")) {
        text = extractData.stripBase64Blocks(text);

        let data = {};
        const prettified = prettier.format(text, {
            parser(text, { babel }) {
                const ast = babel(text);
                extractData.cleanup(ast);
                if (extract)
                    data = extractData.extractData(ast);
                extractData.transformJsx(ast);
                return ast;
            },
            printWidth: 120
        });

        return { text: prettified, data };
    } else if (filename.endsWith(".css")) {
        return { text: prettier.format(text, { parser: "css" }) };
    } else if (filename.endsWith(".html")) {
        return { text: prettier.format(text, { parser: "html" }) };
    }
    return { text };
}

main("repo/").catch(console.dir);