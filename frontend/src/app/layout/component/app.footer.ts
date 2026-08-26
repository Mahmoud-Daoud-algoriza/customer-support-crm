import { Component, inject } from '@angular/core';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';

@Component({
    standalone: true,
    selector: 'app-footer',
    template: `<div class="layout-footer">
        {{ config.productName() }} — UI built on
        <a href="https://github.com/primefaces/sakai-ng" target="_blank" rel="noopener noreferrer" class="text-primary font-bold hover:underline">Sakai</a>
        by
        <a href="https://primeng.org" target="_blank" rel="noopener noreferrer" class="text-primary font-bold hover:underline">PrimeNG</a>
    </div>`
})
export class AppFooter {
    protected readonly config = inject(RuntimeConfigService);
}
