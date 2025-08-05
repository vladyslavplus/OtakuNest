export abstract class QueryStringParameters {
    private readonly maxPageSize = 50;

    pageNumber: number = 1;

    private _pageSize: number = 10;
    get pageSize(): number {
        return this._pageSize;
    }
    set pageSize(value: number) {
        this._pageSize = value > this.maxPageSize ? this.maxPageSize : value;
    }

    orderBy?: string = 'Id';
}
