import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { BookService } from './book.service';

describe('BookService', () => {
  let service: BookService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(BookService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('trims filters and sends pagination', () => {
    service.search('  animal ', ' orwell  ', 2).subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/api/books/search?page=2&pageSize=20&title=animal&author=orwell`,
    );
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], page: 2, pageSize: 20, totalCount: 0, totalPages: 0 });
  });

  it('sends create, retrieve, and delete requests through the management gateway', () => {
    const book = {
      author: { authorId: null, name: 'Test Author' },
      title: 'Test Book',
      subTitle: null,
    };

    service.create(book).subscribe();
    const create = http.expectOne(`${environment.apiUrl}/api/ui/books`);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual(book);
    create.flush({ ...book, bookId: 10 });

    service.get(10).subscribe();
    const get = http.expectOne(`${environment.apiUrl}/api/ui/books/10`);
    expect(get.request.method).toBe('GET');
    get.flush({ ...book, bookId: 10 });

    service.delete(10).subscribe();
    const remove = http.expectOne(`${environment.apiUrl}/api/ui/books/10`);
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null);
  });

  it('loads authors through the management gateway', () => {
    service.authors().subscribe();

    const request = http.expectOne(`${environment.apiUrl}/api/ui/authors`);
    expect(request.request.method).toBe('GET');
    request.flush([{ authorId: 42, name: 'Test Author' }]);
  });
});
