import { parseMarkdown } from './markdown-view.component';

/**
 * The article reader's parser (story 12, T2-E: *"plain text / basic markdown"*).
 *
 * It is tested as a **pure function** rather than through a rendered component because that is where
 * it can be wrong: the template only walks the blocks it is handed. The last two cases are the ones
 * that matter for a customer-facing screen — **markup in a body is text, never markup**, and an
 * unmatched marker stays the character it is instead of swallowing the line.
 */
describe('parseMarkdown', () => {
    it('reads headings, paragraphs and both kinds of list', () => {
        const blocks = parseMarkdown(
            ['## Paying an invoice', '', 'A paragraph.', '', '- one', '- two', '', '1. first', '2. second'].join('\n')
        );

        expect(blocks.map((b) => b.kind)).toEqual(['h2', 'p', 'ul', 'ol']);
        expect(blocks[1].spans[0].text).toBe('A paragraph.');
        expect(blocks[2].items.length).toBe(2);
        expect(blocks[3].items[1][0].text).toBe('second');
    });

    it('joins the lines of one paragraph and separates paragraphs on a blank line', () => {
        const blocks = parseMarkdown('first line\nsecond line\n\nnew paragraph');

        expect(blocks.length).toBe(2);
        expect(blocks[0].spans[0].text).toBe('first line second line');
        expect(blocks[1].spans[0].text).toBe('new paragraph');
    });

    it('reads bold, italic and inline code', () => {
        const spans = parseMarkdown('a **bold** and *italic* and `code` word')[0].spans;

        expect(spans.filter((s) => s.style === 'bold').map((s) => s.text)).toEqual(['bold']);
        expect(spans.filter((s) => s.style === 'italic').map((s) => s.text)).toEqual(['italic']);
        expect(spans.filter((s) => s.style === 'code').map((s) => s.text)).toEqual(['code']);
    });

    it('treats HTML in a body as text, not as markup', () => {
        // The parser emits **spans of text**, and the template interpolates them — so there is no
        // point at which authored content becomes HTML. This is why the reader never uses
        // `innerHTML`: article bodies reach a customer screen through `/portal/help`.
        const spans = parseMarkdown('<script>alert(1)</script>')[0].spans;

        expect(spans.length).toBe(1);
        expect(spans[0].style).toBe('plain');
        expect(spans[0].text).toBe('<script>alert(1)</script>');
    });

    it('leaves an unmatched marker as the character it is', () => {
        const spans = parseMarkdown('2 * 3 is six')[0].spans;

        expect(spans.length).toBe(1);
        expect(spans[0].text).toBe('2 * 3 is six');
    });
});
