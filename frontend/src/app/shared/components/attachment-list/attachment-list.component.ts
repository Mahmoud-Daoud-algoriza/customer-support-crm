import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import { AttachmentMetadata, AttachmentsClient } from '../../../core/api/attachments.client';
import { EmptyStateComponent } from '../empty-state/empty-state.component';

/**
 * `AttachmentList` + uploader — the shared component of docs/ui-design.md §8, used by the ticket,
 * customer and portal surfaces. Story 04 is its first consumer; nothing here is customer-specific.
 *
 * **The bytes come from one place only.** Downloading goes through `AttachmentsClient`, whose URL is
 * built from the attachment **id** — `GET /attachments/{id}/content` (AP-19). No storage path is
 * returned by any endpoint and none could be used here: `AttachmentMetadata` has nowhere to put one
 * (docs/api-design.md §6.7).
 *
 * **`413` surfaces inline on the uploader** (docs/ui-design.md §9), as a translated string chosen by
 * the Problem Details `type` slug; the server's `detail` is never rendered raw.
 *
 * **Nothing here deletes an attachment**, because no endpoint does. Versioning, previews and virus
 * scanning are out of scope by the story's own exclusion list — this component offers no seam for
 * any of them.
 */
@Component({
    selector: 'app-attachment-list',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, EmptyStateComponent, MessageModule, TranslocoModule],
    template: `
        <div class="app-attachments">
            @if (attachments().length === 0) {
                <app-empty-state [title]="'attachments.emptyTitle' | transloco" [message]="'attachments.emptyMessage' | transloco" icon="pi-paperclip" />
            } @else {
                <ul class="app-attachments__list">
                    @for (attachment of attachments(); track attachment.id) {
                        <li class="app-attachments__item">
                            <!-- A non-directional icon: it does not mirror in Arabic
                                 (docs/ui-design.md §10.2). -->
                            <i class="pi pi-paperclip" aria-hidden="true"></i>

                            <span class="app-attachments__name">{{ attachment.fileName }}</span>
                            <span class="app-attachments__meta app-ltr-numeric">{{ formatSize(attachment.sizeBytes) }}</span>
                            <span class="app-attachments__meta">{{ attachment.uploadedBy.displayName }}</span>

                            <p-button type="button" severity="secondary" [text]="true" icon="pi pi-download" [ariaLabel]="'attachments.download' | transloco" [loading]="downloading() === attachment.id" (onClick)="download(attachment)" />
                        </li>
                    }
                </ul>
            }

            @if (downloadProblem(); as failure) {
                <p-message severity="error" [text]="errorKey(failure) | transloco" />
            }

            @if (canUpload()) {
                <div class="app-attachments__uploader">
                    <label class="app-attachments__picker">
                        <span class="app-field__label">{{ 'attachments.add' | transloco }}</span>
                        <input type="file" [disabled]="uploading()" (change)="pick($event)" />
                    </label>

                    @if (capLabel(); as cap) {
                        <p class="app-attachments__meta app-ltr-numeric">{{ 'attachments.cap' | transloco: { size: cap } }}</p>
                    }

                    <!-- 413 is inline on the uploader, with the rest of the region untouched
                         (docs/ui-design.md §9). -->
                    @if (uploadProblem(); as failure) {
                        <p-message severity="error" [text]="errorKey(failure) | transloco" />
                    }
                </div>
            }
        </div>
    `
})
export class AttachmentListComponent {
    private readonly api = inject(AttachmentsClient);

    readonly attachments = input.required<AttachmentMetadata[]>();

    /** The portal and any read-only surface pass `false`; nothing here decides who may upload. */
    readonly canUpload = input(true);

    readonly uploading = input(false);

    /** The owner's upload failure — `413 attachment-too-large` above all (T2-A). */
    readonly uploadProblem = input<ApiProblem | null>(null);

    /**
     * The configured cap, in bytes, when the screen knows it.
     *
     * **It is `null` today, and the hole is deliberate.** docs/ui-design.md §8 asks the uploader to
     * state *"size cap from configuration"*, but **no approved endpoint publishes that value**:
     * `BootstrapConfig`, `CustomerConfig` and `StaffConfig` (docs/api-design.md §6.9) each list
     * their members exhaustively and the cap is in none of them. Adding it would be new contract
     * surface, which is the user's call, not a screen's — recorded as finding **I-12**. Until then
     * the cap line is simply absent and `413` still reads as a translated sentence.
     *
     * *Whoever publishes the cap: pass it here and the line appears. Nothing else changes.*
     */
    readonly maxSizeBytes = input<number | null>(null);

    readonly upload = output<File>();

    protected readonly downloading = signal<string | null>(null);
    protected readonly downloadProblem = signal<ApiProblem | null>(null);

    protected errorKey = problemTranslationKey;

    protected readonly capLabel = computed(() => {
        const cap = this.maxSizeBytes();

        return cap === null ? null : this.formatSize(cap);
    });

    protected pick(event: Event): void {
        const input = event.target as HTMLInputElement;
        const file = input.files?.[0];

        if (!file) {
            return;
        }

        this.upload.emit(file);

        // Clearing lets the same file be chosen again after a failure — without it, re-picking the
        // file that was just rejected raises no change event at all.
        input.value = '';
    }

    /**
     * Fetches the bytes through the authorized endpoint and hands them to the browser.
     *
     * A plain link to the same path would send no `Authorization` header and be answered `401` — the
     * token lives in `localStorage`, not in a cookie (AD-7). See finding **I-13**.
     *
     * A caller who cannot reach the owner gets `404`, worded identically to a missing attachment
     * (AP-4). This component does not try to tell the two apart, and must not.
     */
    protected download(attachment: AttachmentMetadata): void {
        if (this.downloading() !== null) {
            return;
        }

        this.downloading.set(attachment.id);
        this.downloadProblem.set(null);

        this.api.download(attachment.id).subscribe({
            next: (blob) => {
                this.downloading.set(null);
                save(blob, attachment.fileName);
            },
            error: (failure: ApiProblem) => {
                this.downloading.set(null);
                this.downloadProblem.set(failure);
            }
        });
    }

    /**
     * Binary units, because the cap itself is expressed in bytes and a mismatch between what the
     * screen shows and what the server measures would be worse than a rounded number.
     */
    protected formatSize(bytes: number): string {
        if (bytes < 1024) {
            return `${bytes} B`;
        }

        const units = ['KB', 'MB', 'GB'];
        let value = bytes / 1024;
        let unit = 0;

        while (value >= 1024 && unit < units.length - 1) {
            value /= 1024;
            unit += 1;
        }

        return `${value.toFixed(1)} ${units[unit]}`;
    }
}

/**
 * The original client-supplied file name survives the round trip; the name on disk never leaves the
 * server (docs/api-design.md §6.7).
 */
function save(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');

    anchor.href = url;
    anchor.download = fileName;
    anchor.click();

    URL.revokeObjectURL(url);
}
