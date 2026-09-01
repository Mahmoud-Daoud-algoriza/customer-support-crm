import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Renders the **basic markdown** an article body may contain (T2-E: *"plain text / basic
 * markdown"*).
 *
 * <h3>It parses to a model and renders through the template — never `innerHTML`</h3>
 * Article bodies are authored content that reaches a **customer** screen through `/portal/help`.
 * Building an HTML string and binding it would mean either trusting authored text or reaching for
 * `bypassSecurityTrust*`; parsing into blocks and spans and letting Angular render them means
 * markup in a body is displayed as the text it is, with no sanitizer decision to get wrong.
 *
 * <h3>What is supported, and why the list is short</h3>
 * Headings (`#`–`###`), unordered and ordered lists, paragraphs, and inline `**bold**`, `*italic*`
 * and `` `code` ``. **No images, no embedded HTML, no tables and no links to uploaded files** —
 * T2-E excludes a rich editor and a media library, so rendering support for them would advertise an
 * authoring capability that does not exist.
 *
 * <h3>Content is never translated</h3>
 * **A-11**: UI chrome is translated, user-generated content is stored as authored. Nothing here
 * passes a body through Transloco, and it must not — an Arabic interface renders an English article
 * unchanged, which is the intended behaviour rather than a gap.
 *
 * The rendered text inherits the page direction, so an Arabic body reads right-to-left in either
 * interface language without a direction-specific rule (docs/ui-design.md §10.2).
 */
@Component({
    selector: 'app-markdown-view',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgTemplateOutlet],
    template: `
        <div class="app-markdown">
            @for (block of blocks(); track $index) {
                @switch (block.kind) {
                    @case ('h1') {
                        <h3 class="app-markdown__h1">
                            <ng-container [ngTemplateOutlet]="inline" [ngTemplateOutletContext]="{ spans: block.spans }" />
                        </h3>
                    }
                    @case ('h2') {
                        <h4 class="app-markdown__h2">
                            <ng-container [ngTemplateOutlet]="inline" [ngTemplateOutletContext]="{ spans: block.spans }" />
                        </h4>
                    }
                    @case ('h3') {
                        <h5 class="app-markdown__h3">
                            <ng-container [ngTemplateOutlet]="inline" [ngTemplateOutletContext]="{ spans: block.spans }" />
                        </h5>
                    }
                    @case ('ul') {
                        <ul class="app-markdown__list">
                            @for (item of block.items; track $index) {
                                <li><ng-container [ngTemplateOutlet]="inline" [ngTemplateOutletContext]="{ spans: item }" /></li>
                            }
                        </ul>
                    }
                    @case ('ol') {
                        <ol class="app-markdown__list">
                            @for (item of block.items; track $index) {
                                <li><ng-container [ngTemplateOutlet]="inline" [ngTemplateOutletContext]="{ spans: item }" /></li>
                            }
                        </ol>
                    }
                    @default {
                        <p class="app-markdown__p">
                            <ng-container [ngTemplateOutlet]="inline" [ngTemplateOutletContext]="{ spans: block.spans }" />
                        </p>
                    }
                }
            }
        </div>

        <ng-template #inline let-spans="spans">
            @for (span of spans; track $index) {
                @switch (span.style) {
                    @case ('bold') {
                        <strong>{{ span.text }}</strong>
                    }
                    @case ('italic') {
                        <em>{{ span.text }}</em>
                    }
                    @case ('code') {
                        <code class="app-markdown__code">{{ span.text }}</code>
                    }
                    @default {
                        {{ span.text }}
                    }
                }
            }
        </ng-template>
    `
})
export class MarkdownViewComponent {
    readonly source = input.required<string>();

    protected readonly blocks = computed(() => parseMarkdown(this.source()));
}

type BlockKind = 'h1' | 'h2' | 'h3' | 'p' | 'ul' | 'ol';

type SpanStyle = 'plain' | 'bold' | 'italic' | 'code';

interface Span {
    text: string;
    style: SpanStyle;
}

interface Block {
    kind: BlockKind;
    spans: Span[];
    items: Span[][];
}

/**
 * Line-oriented, which is all "basic markdown" needs: a heading, a list item or a run of prose is
 * decided by the start of a line, and blank lines separate paragraphs.
 */
export function parseMarkdown(source: string): Block[] {
    const blocks: Block[] = [];
    const lines = (source ?? '').replace(/\r\n/g, '\n').split('\n');

    let paragraph: string[] = [];
    let list: { kind: 'ul' | 'ol'; items: string[] } | null = null;

    const flushParagraph = () => {
        if (paragraph.length > 0) {
            blocks.push({ kind: 'p', spans: parseInline(paragraph.join(' ')), items: [] });
            paragraph = [];
        }
    };

    const flushList = () => {
        if (list) {
            blocks.push({ kind: list.kind, spans: [], items: list.items.map(parseInline) });
            list = null;
        }
    };

    for (const line of lines) {
        const trimmed = line.trim();

        if (trimmed === '') {
            flushParagraph();
            flushList();
            continue;
        }

        const heading = /^(#{1,3})\s+(.*)$/.exec(trimmed);
        if (heading) {
            flushParagraph();
            flushList();
            blocks.push({
                kind: `h${heading[1].length}` as BlockKind,
                spans: parseInline(heading[2]),
                items: []
            });
            continue;
        }

        const bullet = /^[-*]\s+(.*)$/.exec(trimmed);
        if (bullet) {
            flushParagraph();
            if (list?.kind !== 'ul') {
                flushList();
                list = { kind: 'ul', items: [] };
            }
            list.items.push(bullet[1]);
            continue;
        }

        const numbered = /^\d+[.)]\s+(.*)$/.exec(trimmed);
        if (numbered) {
            flushParagraph();
            if (list?.kind !== 'ol') {
                flushList();
                list = { kind: 'ol', items: [] };
            }
            list.items.push(numbered[1]);
            continue;
        }

        flushList();
        paragraph.push(trimmed);
    }

    flushParagraph();
    flushList();

    return blocks;
}

/**
 * `**bold**`, `*italic*` and `` `code` ``. Unmatched markers stay as the characters they are, so a
 * body containing a lone asterisk reads as an asterisk rather than swallowing the rest of the line.
 */
function parseInline(text: string): Span[] {
    const spans: Span[] = [];
    const pattern = /(\*\*[^*]+\*\*|\*[^*]+\*|`[^`]+`)/g;

    let index = 0;
    let match = pattern.exec(text);

    while (match) {
        if (match.index > index) {
            spans.push({ text: text.slice(index, match.index), style: 'plain' });
        }

        const token = match[0];

        if (token.startsWith('**')) {
            spans.push({ text: token.slice(2, -2), style: 'bold' });
        } else if (token.startsWith('`')) {
            spans.push({ text: token.slice(1, -1), style: 'code' });
        } else {
            spans.push({ text: token.slice(1, -1), style: 'italic' });
        }

        index = match.index + token.length;
        match = pattern.exec(text);
    }

    if (index < text.length) {
        spans.push({ text: text.slice(index), style: 'plain' });
    }

    return spans;
}
