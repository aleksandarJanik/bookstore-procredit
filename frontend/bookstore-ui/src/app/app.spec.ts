import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { App } from './app';
import { AuthService } from './core/auth.service';
import { BookService } from './core/book.service';

const auth = {
  authenticated: signal(false),
  username: signal<string | null>(null),
  unavailable: signal(false),
  login: vi.fn(),
  logout: vi.fn(),
};

const books = {
  search: vi.fn(() => of({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })),
  authors: vi.fn(() => of([{ authorId: 42, name: 'Existing Author' }])),
  create: vi.fn(() =>
    of({
      bookId: 10,
      author: { authorId: 42, name: 'Existing Author' },
      title: 'Test Book',
      subTitle: null,
    }),
  ),
  get: vi.fn(),
  delete: vi.fn(),
};

describe('App', () => {
  beforeEach(async () => {
    auth.authenticated.set(false);
    auth.username.set(null);
    auth.unavailable.set(false);
    vi.clearAllMocks();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: BookService, useValue: books },
      ],
    })
      .compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should explain the catalogue search', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Find the book');
  });

  it('shows create, retrieve, and delete management controls when signed in', async () => {
    auth.authenticated.set(true);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('.create-card button[type="submit"]')?.textContent).toContain('Add to catalogue');
    expect(compiled.querySelector('.retrieve-card button[type="submit"]')?.textContent).toContain('Retrieve book');
    expect(compiled.querySelector('.management-area')?.textContent).toContain('Retrieve & delete');
    expect(compiled.querySelector('input[formControlName="authorId"]')).toBeNull();
    expect(compiled.querySelector('select[formControlName="authorId"]')).not.toBeNull();
  });

  it('submits an existing author without asking the user for its ID', async () => {
    auth.authenticated.set(true);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    const title = compiled.querySelector<HTMLInputElement>('.create-card input[formControlName="title"]')!;
    title.value = 'Existing Author Book';
    title.dispatchEvent(new Event('input'));
    compiled.querySelector<HTMLFormElement>('.create-card')!.dispatchEvent(new Event('submit'));

    expect(books.create).toHaveBeenCalledWith({
      author: { authorId: 42, name: 'Existing Author' },
      title: 'Existing Author Book',
      subTitle: null,
    });
  });

  it('submits a new author without an ID and selects the generated author', async () => {
    books.create.mockReturnValueOnce(
      of({
        bookId: 11,
        author: { authorId: 84, name: 'New Author' },
        title: 'New Author Book',
        subTitle: null,
      }),
    );
    auth.authenticated.set(true);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    const newMode = compiled.querySelector<HTMLInputElement>('input[value="new"]')!;
    newMode.click();
    fixture.detectChanges();

    const author = compiled.querySelector<HTMLInputElement>('input[formControlName="authorName"]')!;
    author.value = 'New Author';
    author.dispatchEvent(new Event('input'));
    const title = compiled.querySelector<HTMLInputElement>('.create-card input[formControlName="title"]')!;
    title.value = 'New Author Book';
    title.dispatchEvent(new Event('input'));
    compiled.querySelector<HTMLFormElement>('.create-card')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(books.create).toHaveBeenCalledWith({
      author: { authorId: null, name: 'New Author' },
      title: 'New Author Book',
      subTitle: null,
    });
    expect(
      compiled.querySelector<HTMLSelectElement>('select[formControlName="authorId"]')?.selectedOptions[0].textContent,
    ).toContain('New Author');
  });
});
