const traverse = require("@babel/traverse").default;
const t = require("@babel/types");
const jsxConvert = require("./react_transform");

function isCreateElement(path) {
    if (!path.isCallExpression()) return false;
    if (path.node.arguments.length < 2) return false;
    return path.get("callee.property").isIdentifier({ name: "createElement" });
}

function replaceWithSourceString(path, source) {
    path.replaceWithSourceString(source);
    
    // need to do this otherwise prettier explodes
    path.node.trailingComments = null;
    path.node.leadingComments = null;
    path.node.innerComments = null;
}

function getJsxProps(path) {
    if (!isCreateElement(path)) return null;
    if (!path.get("arguments.1").isObjectExpression()) return {};

    return tryEvaluateObject(path.get("arguments.1"));
}

function tryEvaluateObject(path) {
    const obj = {};
    for (const prop of path.get("properties")) {
        if (!prop.get("key").isIdentifier()) continue;
        
        // Workaround for light mode class checks (will probably break things lol)
        let valuePath = prop.get("value");
        if (valuePath.isBinaryExpression({ operator: "+" })) {
            valuePath = valuePath.get("left");
        }

        const key = prop.node.key.name;
        const { confident, value } = valuePath.evaluate();
        if (key && confident) obj[key] = value;
    }
    return obj;
}

function extractBookText(path) {
    let bookText = "";
    path.traverse({
        CallExpression(path) {
            if (!isCreateElement(path)) return;
            const props = getJsxProps(path);
            
            // If this is a "censored" block, print the contents in code blocks
            if (props.str) bookText += "`" + props.str + "`";

            // Handle heading newlines and such
            if (props.className) {
                if (props.className.indexOf("TheBook-Header") > -1)
                    bookText += "# ";

                if (props.className === "TheBook-Subheader")
                    bookText += "\n\n## ";
                
                if (props.className.indexOf("TheBook-Bullet") > -1)
                    bookText += "\n\n### ";
                
                if (props.className.indexOf("TheBook-SubBullet") > -1)
                    bookText += "\n* ";
            }
        },
        StringLiteral(path) {
            // Should be part of a React element
            if (!isCreateElement(path.parentPath)) return;

            // This is just an element name (eg. 'div', skip)
            if (path.key === 0) return;

            // Find parent element of this literal
            const parent = path.findParent((p) => isCreateElement(p));

            const elementType = t.isStringLiteral(parent.node.arguments[0])
                ? parent.node.arguments[0].value
                : null;

            // Output the string literal (w/ formatting)
            if (elementType === "button") {
            } else if (elementType === "del") bookText += `~~${path.node.value}~~`;
            else bookText += path.node.value;
        },
    });

    return bookText;
}

function tryExtractWeather(path) {
    const data = [];
    for (const child of path.get("elements")) {
        // All children must be object expressions
        if (!child.isObjectExpression()) return null;

        // All children must have name, background, color
        const obj = tryEvaluateObject(child);
        if (obj.name && obj.background && obj.color) data.push(obj);
        else return null;
    }

    // Array must not be empty
    return data.length ? data : null;
}

module.exports = {
    stripBase64Blocks(text) {
        return text.replace(/data:([a-z-\/]+);base64,[A-Za-z0-9=+\/]+/g, "<$1 blob>");
    },
    cleanup(ast) {
        traverse(ast, {
            CallExpression(path) {
                if (
                    path.get("callee").isMemberExpression() &&
                    path.get("callee.object").isIdentifier({ name: "JSON" }) &&
                    path.get("callee.property").isIdentifier({ name: "parse" }) &&
                    path.get("arguments.0").isStringLiteral()
                ) {
                    const value = path.node.arguments[0].value;
                    replaceWithSourceString(path, value);
                    return;
                }
            },
        });
    },
    transformJsx(ast) {
        traverse(ast, jsxConvert);
    },
    extractData(ast) {
        const data = {
            book: "",
            attributes: [],
            items: [],
            weather: [],
            glossary: null,
            snackTiers: null,
            library: null
        };

        traverse(ast, {
            CallExpression(path) {
                const props = getJsxProps(path);

                // Extract book text - pre-Expansion book has all content as child of
                // TheBook-All so we need to make sure it doesn't catch the subheader-y tree
                // as well (hence shouldSkip = true)
                if (isCreateElement(path) && props.className === "TheBook-All") {
                    data.book += extractBookText(path.parentPath);
                    path.shouldSkip = true;
                } else if (
                    isCreateElement(path) &&
                    props.className === "TheBook-Subheader"
                ) {
                    data.book += extractBookText(path.parentPath);
                }
            },
            ArrayExpression(path) {
                // Find weather array
                const weather = tryExtractWeather(path);
                if (weather) data.weather = weather;
            },
            AssignmentExpression(path) {
                // Find some "exports" data
                if (path.get("left.property").isIdentifier({ name: "exports" })) {
                    const { confident, value } = path.get("right").evaluate();
                    if (!confident) return;
                    
                    // We have data, find out what it is...
                    if (value.collection) {
                        for (const item of value.collection) {
                            if (item.id && item.description !== undefined) {
                                // This is an attribute
                                data.attributes.push(item);
                            } else if (item.id && item.attr !== undefined) {
                                // This is an item
                                data.items.push(item);
                            }
                        }
                    } else if (value.glossary) {
                        data.glossary = value.glossary;
                    } else if (value.maxBetTiers) {
                        data.snackTiers = value;
                    } else if (value.books) {
                        data.library = value.books;
                    }
                }
            },
        });
        
        // Strip the unnecessary glossary line I can't figure out how to get rid of in a clean way
        data.book = data.book.replace("## Glossary:\xa0", "").trim();

        return data;
    },
};