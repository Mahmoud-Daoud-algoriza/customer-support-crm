import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { Department, OrganizationClient } from '../../../core/api/organization.client';
import { AuthStore } from '../../../core/auth/auth.store';
import { Identity, UserRole } from '../../../core/auth/identity.model';
import { DepartmentFilterComponent } from './department-filter.component';

/**
 * Story 03 verification step 6, as far as this story can carry it.
 *
 * The step is written against `/workspace/tickets`, which **Story 05 delivers** — so the screen it
 * names does not exist yet. What does exist is the rule itself, and Story 03 task 8 put it in
 * exactly one place so the ticket list cannot re-implement it. That place is what these tests
 * exercise: *fixed to their own department and disabled* for an `Agent`, *enabled across all
 * departments* for a `Manager` (docs/ui-design.md §5.2).
 *
 * **Story 05 must still run the step against the real ticket list.** This is the unit half.
 */
describe('DepartmentFilterComponent', () => {
    const BILLING = '11111111-1111-1111-1111-111111111101';
    const TECHNICAL = '11111111-1111-1111-1111-111111111102';

    const DEPARTMENTS: Department[] = [
        { id: BILLING, name: 'Billing' },
        { id: TECHNICAL, name: 'Technical' }
    ];

    let auth: AuthStore;

    async function renderAs(role: UserRole, departmentId: string | null, disabledForOwnDepartment: boolean): Promise<ComponentFixture<DepartmentFilterComponent>> {
        TestBed.configureTestingModule({
            imports: [DepartmentFilterComponent, TranslocoTestingModule.forRoot({ langs: { en: {} }, translocoConfig: { availableLangs: ['en'], defaultLang: 'en' } })],
            // p-message carries a synthetic animation, so an animations provider is required even
            // though nothing here asserts on it.
            providers: [provideNoopAnimations(), { provide: OrganizationClient, useValue: { getDepartments: () => of(DEPARTMENTS) } }]
        });

        auth = TestBed.inject(AuthStore);
        auth.setIdentity({ id: 'u1', displayName: 'Test', email: 't@local', role, departmentId, isActive: true } as Identity);

        const fixture = TestBed.createComponent(DepartmentFilterComponent);
        fixture.componentRef.setInput('disabledForOwnDepartment', disabledForOwnDepartment);
        await fixture.whenStable();
        fixture.detectChanges();

        return fixture;
    }

    afterEach(() => TestBed.resetTestingModule());

    it('pins an Agent to their own department and disables the control', async () => {
        const fixture = await renderAs('Agent', BILLING, true);

        // The value is pinned, not merely greyed out: a disabled control still reading "any
        // department" would describe results the agent is not actually seeing.
        expect(fixture.componentInstance.value()).toBe(BILLING);

        const select: HTMLElement = fixture.nativeElement.querySelector('p-select');
        expect(select.classList.contains('p-disabled')).toBeTrue();

        // And the hint, so architecture §4.3 is legible rather than mysterious.
        expect(fixture.nativeElement.querySelector('p-message')).not.toBeNull();
    });

    it('leaves a Manager enabled across all departments', async () => {
        const fixture = await renderAs('Manager', BILLING, true);

        // Not pinned: a Manager sees every department, so the filter starts at "any".
        expect(fixture.componentInstance.value()).toBeNull();

        const select: HTMLElement = fixture.nativeElement.querySelector('p-select');
        expect(select.classList.contains('p-disabled')).toBeFalse();

        expect(fixture.nativeElement.querySelector('p-message')).toBeNull();
    });

    it('leaves an Administrator enabled across all departments', async () => {
        const fixture = await renderAs('Administrator', BILLING, true);

        expect(fixture.componentInstance.value()).toBeNull();
        expect((fixture.nativeElement.querySelector('p-select') as HTMLElement).classList.contains('p-disabled')).toBeFalse();
    });

    it('never locks an Agent when the screen does not ask for it', async () => {
        // The lock is the ticket list's rule (ui-design §5.2), not a general one. `/admin/users`
        // and `/workspace/reports` leave `disabledForOwnDepartment` off.
        const fixture = await renderAs('Agent', BILLING, false);

        expect(fixture.componentInstance.value()).toBeNull();
        expect((fixture.nativeElement.querySelector('p-select') as HTMLElement).classList.contains('p-disabled')).toBeFalse();
    });
});
