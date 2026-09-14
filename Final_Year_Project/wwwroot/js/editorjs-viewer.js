(function (global) {
    function edjsHTML() {
        return {
            parse: (data) => {
                if (!data || !data.blocks) return [];
                return data.blocks.map(block => {
                    console.log("Block:", block);
                    switch (block.type) {
                        case "header":
                            const level = block.data.level || 1;
                            return `<h${level}>${block.data.text}</h${level}>`;

                        case "paragraph":
                            return `<p>${block.data.text}</p>`;

                        case "list":
                            const tag = block.data.style === "ordered" ? "ol" : "ul";
                            const items = (block.data.items || [])
                                .map(i => `<li>${i}</li>`).join("");
                            return `<${tag}>${items}</${tag}>`;

                        case "linkTool":
                            const link = block.data?.link || "#";
                            const meta = block.data?.meta || {};
                            const title = meta.title ? `<div class="text-base font-semibold truncate">${meta.title}</div>` : "";
                            const description = meta.description
                                ? `<div class="text-sm text-gray-600 line-clamp-2">${meta.description}</div>`
                                : "";
                            const domain = new URL(link).hostname.replace(/^www\./, "");
                            const image = meta.image?.url
                                ? `<img src="${meta.image.url}" alt="preview" class="w-16 h-16 object-cover rounded-md flex-shrink-0" />`
                                : "";

                            return `
                                <a href="${link}" target="_blank" rel="noopener"
                                   class="block border border-gray-300 rounded-lg p-3 hover:bg-gray-50 transition overflow-hidden max-w-full">
                                  <div class="flex items-center gap-3">
                                    ${image}
                                    <div class="min-w-0 flex-1">
                                      ${title}
                                      ${description}
                                      <div class="text-xs text-gray-500 truncate mt-1">${domain}</div>
                                    </div>
                                  </div>
                                </a>
                              `;

                        case "image":
                            return `
                                <figure class="my-4">
                                  <img src="${block.data.file.url}" alt="${block.data.caption || ""}"mx-auto max-w-full" />
                                  ${block.data.caption ? `<figcaption class="text-sm text-gray-500 text-center mt-2">${block.data.caption}</figcaption>` : ""}
                                </figure>
                              `;

                        case "attaches":
                            const fileName = block.data.file?.name || "Download File";
                            const fileSizeBytes = block.data.file?.size || 0;
                            const fileUrl = block.data.file?.url || "#";

                            // Convert bytes to readable format
                            let displaySize;
                            if (fileSizeBytes < 1024) {
                                displaySize = fileSizeBytes + " B";
                            } else if (fileSizeBytes < 1048576) { // less than 1 MB
                                displaySize = (fileSizeBytes / 1024).toFixed(1) + " KB";
                            } else {
                                displaySize = (fileSizeBytes / 1048576).toFixed(2) + " MB";
                            }

                            return `
                            <div class="bg-base-200 rounded-lg p-6 text-center my-4">
                                <p class="font-semibold text-lg text-gray-800 mb-3">${fileName}</p>
                                <a href="${fileUrl}" download="${fileName}" class="btn btn-primary rounded-md text-white">
                                    Download (${displaySize})
                                </a>
                            </div>
                        `;

                        default:
                            return "";
                    }
                });
            }
        };
    }

    global.edjsHTML = edjsHTML;
})(window);
