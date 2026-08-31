import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import { AttachmentMetadata } from '../../../core/api/attachments.client';
import { Customer, CustomerNote, CustomersClient, TimelineEntry } from '../../../core/api/customers.client';
import { Branch, OrganizationClient } from '../../../core/api/organization.client';
import { AttachmentListComponent } from '../../../shared/components/attachment-list/attachment-list.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';

/**
 * Customer detail — `/workspace/customers/:id` (docs/ui-design.md §5.5). Agent+.
 *
 * **Four regions, each with its own loading, empty and error state** (§9): profile, interaction
 * timeline, notes, attachments. They load independently, so one slow call never blanks the screen
 * and one failed call never hides the other three.
 *
 * Deep-linkable: it loads its own data and depends on nothing carried from the directory (§2).
 *
 * **The email field is editable and states its consequence before the save (A-19).** Changing a
 * customer's email also changes the sign-in address of their portal login, and A-9 provides no
 * account-recovery flow, so the helper line is a *persistent line on the field* — not a toast and
 * not a post-save confirmation, because the point is to be read beforehand. It is **unconditional**:
 * the `Customer` payload carries no field saying whether a login exists, and this story adds none.
 *
 * **Neither `409` is a save.** `customer-email-in-use` (another customer holds it) and
 * `user-already-exists` (another user holds it, staff included) both mean the address is taken and
 * both leave every row untouched, so both render **inline on the email field** (§5.5).
 *
 * **`externalReference` is displayed read-only and is sent by nothing here** — the ERP seam field is
 * settable through no endpoint (DM-6, docs/api-design.md §8.3).
 */
@Component({
    selector: 'app-customer-detail',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [AttachmentListComponent, ButtonModule, DatePipe, EmptyStateComponent, ErrorStateComponent, FormsModule, InputTextModule, LoadingStateComponent, MessageModule, RouterLink, SelectModule, TextareaModule, TranslocoModule],
    template: `
        <section class="app-page">
            <a routerLink="/workspace/customers">{{ 'actions.back' | transloco }}</a>

            <!-- ---------------------------------------------------------- Profile -->
            @if (profileProblem(); as failure) {
                <app-error-state [problem]="failure" (retry)="loadProfile()" />
            } @else {
                @if (customer(); as row) {
                    <header class="app-page__header">
                        <h1 class="app-page__title">{{ row.fullName }}</h1>
                    </header>

                    <p class="app-page__meta">
                        {{ 'customers.since' | transloco }}
                        <span class="app-ltr-numeric">{{ row.createdAt | date: 'medium' }}</span>
                        @if (row.externalReference) {
                            · {{ 'customers.externalReference' | transloco }}
                            <span class="app-ltr-numeric">{{ row.externalReference }}</span>
                        }
                    </p>

                    @if (saved()) {
                        <p-message severity="success" [text]="'customers.saved' | transloco" />
                    }

                    <form class="app-form" (ngSubmit)="save()">
                        <label class="app-field">
                            <span class="app-field__label">{{ 'customers.name' | transloco }}</span>
                            <input pInputText name="fullName" required [(ngModel)]="fullName" />
                        </label>

                        <label class="app-field">
                            <span class="app-field__label">{{ 'customers.email' | transloco }}</span>
                            <input pInputText name="email" type="email" required [(ngModel)]="email" />

                            <!-- Persistent, unconditional, and read before the save — A-19,
                                 docs/ui-design.md §5.5. -->
                            <small class="app-field__help">{{ 'customers.emailIsSignIn' | transloco }}</small>

                            @if (emailProblem(); as failure) {
                                <p-message severity="error" [text]="errorKey(failure) | transloco" />
                            }
                        </label>

                        <label class="app-field">
                            <span class="app-field__label">{{ 'customers.phone' | transloco }}</span>
                            <input pInputText name="phone" [(ngModel)]="phone" />
                        </label>

                        <label class="app-field">
                            <span class="app-field__label">{{ 'customers.branch' | transloco }}</span>
                            <p-select name="branchId" [options]="branches()" [(ngModel)]="branchId" optionLabel="name" optionValue="id" [placeholder]="'customers.selectBranch' | transloco" />
                        </label>

                        @if (saveProblem(); as failure) {
                            <p-message severity="error" [text]="errorKey(failure) | transloco" />
                        }

                        <div class="app-form__actions">
                            <p-button type="submit" [label]="'actions.save' | transloco" [loading]="saving()" [disabled]="saving() || branchId === null" />
                        </div>
                    </form>
                } @else {
                    <app-loading-state [rowCount]="4" />
                }
            }

            <!-- ------------------------------------------------ Interaction timeline -->
            <section class="app-region">
                <h2 class="app-region__title">{{ 'customers.timeline.title' | transloco }}</h2>

                @if (timelineProblem(); as failure) {
                    <app-error-state [problem]="failure" (retry)="loadTimeline()" />
                } @else {
                    @if (timeline(); as entries) {
                        @if (entries.length === 0) {
                            <!-- Not an error: a customer with no tickets simply has no activity
                                 (docs/ui-design.md §9). **Story 06 made this region real** — the
                                 server now projects TicketActivity — so an empty state here is a
                                 fact about the data rather than about the schema. -->
                            <app-empty-state [title]="'customers.timeline.emptyTitle' | transloco" icon="pi-clock" />
                        } @else {
                            <ol class="app-timeline">
                                @for (entry of entries; track $index) {
                                    <li class="app-timeline__entry">
                                        <span class="app-timeline__when app-ltr-numeric">{{ entry.occurredAt | date: 'short' }}</span>
                                        <!-- The activity type is a stable CODE (api-design §2);
                                             the label comes from the dictionary Story 06 added, the
                                             same one the ticket history region reads (T2-J). -->
                                        <span class="app-timeline__what">{{ entry.ticketSubject }} · {{ 'tickets.activityType.' + entry.activityType | transloco }}</span>
                                        <!-- Absent exactly when the actor is the SLA monitor. -->
                                        @if (entry.actor) {
                                            <span class="app-timeline__who">{{ entry.actor.displayName }}</span>
                                        }
                                    </li>
                                }
                            </ol>
                        }
                    } @else {
                        <app-loading-state [rowCount]="3" />
                    }
                }
            </section>

            <!-- ------------------------------------------------------------- Notes -->
            <section class="app-region">
                <h2 class="app-region__title">{{ 'customers.notes.title' | transloco }}</h2>

                @if (notesProblem(); as failure) {
                    <app-error-state [problem]="failure" (retry)="loadNotes()" />
                } @else {
                    @if (notes(); as rows) {
                        @if (rows.length === 0) {
                            <app-empty-state [title]="'customers.notes.emptyTitle' | transloco" [message]="'customers.notes.emptyMessage' | transloco" icon="pi-comment" />
                        } @else {
                            <ul class="app-notes">
                                @for (note of rows; track note.id) {
                                    <!-- No edit control and no delete control, anywhere: a note is
                                         immutable once written (docs/data-model.md §2.5) and the server
                                         publishes no path that would change one. -->
                                    <li class="app-notes__item">
                                        <p class="app-notes__body">{{ note.body }}</p>
                                        <p class="app-notes__meta">
                                            {{ note.author.displayName }} ·
                                            <span class="app-ltr-numeric">{{ note.createdAt | date: 'medium' }}</span>
                                        </p>
                                    </li>
                                }
                            </ul>
                        }

                        <form class="app-form" (ngSubmit)="addNote()">
                            <label class="app-field">
                                <span class="app-field__label">{{ 'customers.notes.add' | transloco }}</span>
                                <textarea pTextarea name="noteBody" rows="3" [(ngModel)]="noteBody"></textarea>
                            </label>

                            @if (noteProblem(); as failure) {
                                <p-message severity="error" [text]="errorKey(failure) | transloco" />
                            }

                            <div class="app-form__actions">
                                <p-button type="submit" [label]="'customers.notes.save' | transloco" [loading]="addingNote()" [disabled]="addingNote() || noteBody.trim() === ''" />
                            </div>
                        </form>
                    } @else {
                        <app-loading-state [rowCount]="3" />
                    }
                }
            </section>

            <!-- ------------------------------------------------------- Attachments -->
            <section class="app-region">
                <h2 class="app-region__title">{{ 'customers.attachments.title' | transloco }}</h2>

                @if (attachmentsProblem(); as failure) {
                    <app-error-state [problem]="failure" (retry)="loadAttachments()" />
                } @else {
                    @if (attachments(); as files) {
                        <app-attachment-list [attachments]="files" [uploading]="uploading()" [uploadProblem]="uploadProblem()" (upload)="upload($event)" />
                    } @else {
                        <app-loading-state [rowCount]="2" />
                    }
                }
            </section>
        </section>
    `
})
export class CustomerDetailComponent {
    private readonly api = inject(CustomersClient);
    private readonly organization = inject(OrganizationClient);
    private readonly route = inject(ActivatedRoute);

    private readonly customerId = this.route.snapshot.paramMap.get('id') ?? '';

    protected readonly branches = signal<Branch[]>([]);

    protected readonly customer = signal<Customer | null>(null);
    protected readonly profileProblem = signal<ApiProblem | null>(null);
    protected readonly saveProblem = signal<ApiProblem | null>(null);
    protected readonly saving = signal(false);
    protected readonly saved = signal(false);

    protected readonly timeline = signal<TimelineEntry[] | null>(null);
    protected readonly timelineProblem = signal<ApiProblem | null>(null);

    protected readonly notes = signal<CustomerNote[] | null>(null);
    protected readonly notesProblem = signal<ApiProblem | null>(null);
    protected readonly noteProblem = signal<ApiProblem | null>(null);
    protected readonly addingNote = signal(false);

    protected readonly attachments = signal<AttachmentMetadata[] | null>(null);
    protected readonly attachmentsProblem = signal<ApiProblem | null>(null);
    protected readonly uploadProblem = signal<ApiProblem | null>(null);
    protected readonly uploading = signal(false);

    protected fullName = '';
    protected email = '';
    protected phone = '';
    protected branchId: string | null = null;
    protected noteBody = '';

    protected errorKey = problemTranslationKey;

    constructor() {
        this.organization.getBranches().subscribe((branches) => this.branches.set(branches));

        // Four independent loads, deliberately not chained (docs/ui-design.md §9).
        this.loadProfile();
        this.loadTimeline();
        this.loadNotes();
        this.loadAttachments();
    }

    /**
     * Both `409`s belong on the email field: each means the address is taken, and neither saved
     * anything (docs/ui-design.md §5.5). They are distinct slugs because the collisions are
     * distinct — another *customer* against another *user* — and PF-6's existing slug is reused for
     * PF-6's existing rule rather than a new one being minted.
     */
    protected emailProblem(): ApiProblem | null {
        const failure = this.saveProblem();

        return failure && EMAIL_CONFLICT_TYPES.includes(failure.type) ? failure : null;
    }

    protected loadProfile(): void {
        this.customer.set(null);
        this.profileProblem.set(null);
        this.saved.set(false);

        this.api.getCustomer(this.customerId).subscribe({
            next: (row) => {
                this.customer.set(row);
                this.fullName = row.fullName;
                this.email = row.email;
                this.phone = row.phone ?? '';
                this.branchId = row.branch.id;
            },
            error: (failure: ApiProblem) => this.profileProblem.set(failure)
        });
    }

    protected save(): void {
        if (this.saving() || this.branchId === null) {
            return;
        }

        this.saving.set(true);
        this.saveProblem.set(null);
        this.saved.set(false);

        // The four patchable fields of docs/api-design.md §5.5, and no more. `externalReference` is
        // absent here as it is absent from the request type — a body carrying it is a `400` (AP-10).
        this.api
            .patchCustomer(this.customerId, {
                fullName: this.fullName,
                email: this.email,
                phone: this.phone.trim() === '' ? null : this.phone,
                branchId: this.branchId
            })
            .subscribe({
                next: (row) => {
                    this.saving.set(false);
                    this.customer.set(row);
                    this.saved.set(true);
                },
                error: (failure: ApiProblem) => {
                    this.saving.set(false);
                    this.saveProblem.set(failure);
                }
            });
    }

    protected loadTimeline(): void {
        this.timeline.set(null);
        this.timelineProblem.set(null);

        // Newest first is the server's ordering (docs/api-design.md §5.5); the client does not
        // re-sort, because a page of a differently-sorted list would be meaningless.
        this.api.timeline(this.customerId).subscribe({
            next: (result) => this.timeline.set(result.items),
            error: (failure: ApiProblem) => this.timelineProblem.set(failure)
        });
    }

    protected loadNotes(): void {
        this.notes.set(null);
        this.notesProblem.set(null);

        this.api.notes(this.customerId).subscribe({
            next: (result) => this.notes.set(result.items),
            error: (failure: ApiProblem) => this.notesProblem.set(failure)
        });
    }

    protected addNote(): void {
        if (this.addingNote() || this.noteBody.trim() === '') {
            return;
        }

        this.addingNote.set(true);
        this.noteProblem.set(null);

        this.api.addNote(this.customerId, this.noteBody).subscribe({
            next: () => {
                this.addingNote.set(false);
                this.noteBody = '';
                this.loadNotes();
            },
            error: (failure: ApiProblem) => {
                this.addingNote.set(false);
                this.noteProblem.set(failure);
            }
        });
    }

    protected loadAttachments(): void {
        this.attachments.set(null);
        this.attachmentsProblem.set(null);

        this.api.attachments(this.customerId).subscribe({
            next: (result) => this.attachments.set(result.items),
            error: (failure: ApiProblem) => this.attachmentsProblem.set(failure)
        });
    }

    /** `413 attachment-too-large` comes back here and renders inline on the uploader (§9, T2-A). */
    protected upload(file: File): void {
        if (this.uploading()) {
            return;
        }

        this.uploading.set(true);
        this.uploadProblem.set(null);

        this.api.upload(this.customerId, file).subscribe({
            next: () => {
                this.uploading.set(false);
                this.loadAttachments();
            },
            error: (failure: ApiProblem) => {
                this.uploading.set(false);
                this.uploadProblem.set(failure);
            }
        });
    }
}

/**
 * The two collisions of docs/api-design.md §5.5's A-19 box. Both are rejections that wrote nothing,
 * so both belong on the field that caused them rather than at the top of the form.
 */
const EMAIL_CONFLICT_TYPES = ['customer-email-in-use', 'user-already-exists'];
