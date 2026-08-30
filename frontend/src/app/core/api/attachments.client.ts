import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummary } from '../auth/identity.model';
import { ApiClientBase } from './api-client.base';

/**
 * `AttachmentMetadata` — docs/api-design.md §6.7. Returned by every attachment list and by a
 * successful upload.
 *
 * **`storagePath` is not a property of this type, and that is the point.** §6.7 says it is never
 * returned; the server's DTO has nowhere to put it and neither does this one, so no screen can
 * build a URL from a path even by accident. The bytes come from one place only —
 * `GET /attachments/{attachmentId}/content` (AP-19).
 */
export interface AttachmentMetadata {
    id: string;
    fileName: string;
    contentType: string;
    sizeBytes: number;
    uploadedBy: UserSummary;
    uploadedAt: string;
}

/**
 * `GET /attachments/{attachmentId}/content` — docs/api-design.md §5.5, **AP-19**.
 *
 * One download endpoint for every role. Authorization resolves through the owning ticket or
 * customer on the server, and a caller who cannot reach the owner gets `404` — identical to a
 * missing id (AP-4), which is why no screen may try to tell the two apart.
 *
 * **There is no method here that takes a storage path, and there must never be one.**
 */
@Injectable({ providedIn: 'root' })
export class AttachmentsClient extends ApiClientBase {
    /**
     * The API path of the download, as story 04 task 11 names it. It is the **one** place that path
     * is built, so `download` below and any later caller agree about it.
     *
     * It is deliberately relative to the API base — an id, never a storage path (AP-19, §6.7).
     */
    downloadUrl(attachmentId: string): string {
        return `attachments/${attachmentId}/content`;
    }

    /**
     * The bytes.
     *
     * **Why this exists alongside `downloadUrl`.** The plan reads *"`downloadUrl(attachmentId)`
     * returning the API path (the auth interceptor supplies the bearer token)"*, but the
     * interceptor only sees requests that go through `HttpClient`: this application carries the
     * token in an `Authorization` header read from `localStorage` (AD-7), so a browser navigation
     * to a bare path sends no credential and is answered `401`. Fetching the blob here is what
     * makes the plan's own sentence true. Recorded as finding **I-13**.
     */
    download(attachmentId: string): Observable<Blob> {
        return this.getBlob(this.downloadUrl(attachmentId));
    }
}
