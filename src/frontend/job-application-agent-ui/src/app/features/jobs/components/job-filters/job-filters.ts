import { Component, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ContractType } from '../../models/contract-type.model';

export interface JobFiltersValue {
    keywords: string;
    location: string;
    remoteOnly: boolean;
    contractTypes: ContractType[];
    salaryMin: number | null;
}

@Component({
    selector: 'app-job-filters',
    imports: [FormsModule],
    templateUrl: './job-filters.html',
    styleUrl: './job-filters.scss'
})
export class JobFilters {
    protected readonly ContractType = ContractType;
    readonly filtersChanged = output<JobFiltersValue>();

    protected keywords = '';
    protected location = '';
    protected remoteOnly = false;
    protected contractTypes: ContractType[] = [];
    protected salaryMin: number | null = null;

    protected applyFilters(): void {
        this.filtersChanged.emit({
            keywords: this.keywords,
            location: this.location,
            remoteOnly: this.remoteOnly,
            contractTypes: this.contractTypes,
            salaryMin: this.salaryMin
        });
    }

    protected resetFilters(): void {
        this.keywords = '';
        this.location = '';
        this.remoteOnly = false;
        this.contractTypes = [];
        this.salaryMin = null;

        this.applyFilters();
    }
    protected toggleContractType(
        contractType: ContractType,
        checked: boolean
    ): void {
        this.contractTypes = checked
            ? [...this.contractTypes, contractType]
            : this.contractTypes.filter(
                value => value !== contractType
            );

        this.applyFilters();
    }
}
