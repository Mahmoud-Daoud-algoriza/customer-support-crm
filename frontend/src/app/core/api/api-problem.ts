/**
 * A normalized RFC 9457 Problem Details body (docs/api-design.md §6.12).
 *
 * `type` is a stable slug and it is the **only** field the UI maps to a translated string.
 * `detail` is server prose and is never rendered raw (docs/ui-design.md §9, T2-J).
 */
export interface ApiProblem {
    type: string;
    title: string;
    status: number;
    detail?: string;
    instance?: string;
    errors?: Record<string, string[]>;
    /** `illegal-transition` additionally carries this (docs/api-design.md §6.12). */
    allowedTransitions?: string[];
}

/** Used when the server could not be reached at all, so there is no Problem Details body. */
export const NETWORK_PROBLEM_TYPE = 'network-unavailable';

/**
 * The translation key an `ApiProblem` maps to. Dictionaries live in src/assets/i18n; Story 17
 * Part B completes the wording.
 */
export function problemTranslationKey(problem: ApiProblem): string {
    return `errors.${problem.type}`;
}
