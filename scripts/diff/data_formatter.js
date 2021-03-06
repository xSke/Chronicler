function redact(text) {
    return text.replace(/([|\s]{3,})/g, "`$1`");
}

module.exports = {
    formatAttributes(attributes) {
        let text = "";
        
        console.log(attributes);
        for (const attr of attributes) {
            text += `### ${attr.title} (\`${attr.id}\`)\n`;
            if (attr.description)
                text += attr.description;
            else
                text += "*(no description)*";
            text += "\n\n";
        }
        
        return text;
    },
    formatGlossary(glossary) {
        let text = "";
        for (const item of glossary) {
            text += `## ${redact(item.name)}\n`;
            for (const def of item.definition) {
                text += `- ${redact(def)}\n`;
            }
            
            text += "\n";
        }
        return text;
    }
};