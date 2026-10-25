import { Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { AuthService } from './core/auth.service';
import { Author, Book, BookService, PagedResult } from './core/book.service';

function validAuthor(control: AbstractControl): ValidationErrors | null {
  if (control.get('authorMode')?.value === 'existing') {
    return control.get('authorId')?.value ? null : { authorRequired: true };
  }

  const name = String(control.get('authorName')?.value ?? '').trim();
  return name.length >= 3 && name.length <= 100 ? null : { authorRequired: true };
}

@Component({
  imports: [ReactiveFormsModule],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App implements OnInit {
  private readonly books = inject(BookService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly auth = inject(AuthService);
  protected readonly form = this.formBuilder.nonNullable.group({ title: '', author: '' });
  protected readonly createForm = this.formBuilder.group(
    {
      authorMode: this.formBuilder.nonNullable.control<'existing' | 'new'>('existing'),
      authorId: this.formBuilder.control<number | null>(null),
      authorName: this.formBuilder.nonNullable.control('', Validators.maxLength(100)),
      title: this.formBuilder.nonNullable.control('', [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(100),
      ]),
      subTitle: this.formBuilder.nonNullable.control(''),
    },
    { validators: validAuthor },
  );
  protected readonly lookupForm = this.formBuilder.nonNullable.group({
    bookId: [1, [Validators.required, Validators.min(1)]],
  });
  protected readonly result = signal<PagedResult | null>(null);
  protected readonly authors = signal<Author[]>([]);
  protected readonly selectedBook = signal<Book | null>(null);
  protected readonly loading = signal(false);
  protected readonly managementLoading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly managementError = signal<string | null>(null);
  protected readonly managementMessage = signal<string | null>(null);
  protected readonly authorLoading = signal(false);
  protected readonly authorError = signal<string | null>(null);
  protected readonly deletePending = signal(false);
  protected readonly currentYear = new Date().getFullYear();

  ngOnInit(): void {
    if (this.auth.authenticated()) {
      this.search(1);
      this.loadAuthors();
    }
  }

  protected search(page = 1): void {
    if (!this.auth.authenticated()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const { title, author } = this.form.getRawValue();
    this.books
      .search(title, author, page)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => this.result.set(result),
        error: (error: { status?: number }) => {
          this.error.set(
            error.status === 401 || error.status === 403
              ? 'Your session cannot access the catalogue. Sign in again.'
              : 'The catalogue is unavailable. Check that the API is running and try again.',
          );
        },
      });
  }

  protected clear(): void {
    this.form.reset();
    this.search(1);
  }

  protected createBook(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    const value = this.createForm.getRawValue();
    const selectedAuthor = this.authors().find((author) => author.authorId === value.authorId);
    if (value.authorMode === 'existing' && !selectedAuthor) {
      this.createForm.setErrors({ authorRequired: true });
      this.createForm.markAllAsTouched();
      return;
    }

    this.runManagementRequest(
      this.books.create({
        author:
          value.authorMode === 'existing'
            ? { authorId: selectedAuthor!.authorId, name: selectedAuthor!.name }
            : { authorId: null, name: value.authorName.trim() },
        title: value.title.trim(),
        subTitle: value.subTitle.trim() || null,
      }),
      (book) => {
        if (!this.authors().some((author) => author.authorId === book.author.authorId)) {
          this.authors.update((authors) =>
            [...authors, book.author].sort((left, right) => left.name.localeCompare(right.name)),
          );
        }
        this.createForm.reset({
          authorMode: 'existing',
          authorId: book.author.authorId,
          authorName: '',
          title: '',
          subTitle: '',
        });
        this.selectedBook.set(book);
        this.lookupForm.controls.bookId.setValue(book.bookId);
        this.managementMessage.set(`“${book.title}” was added as book ${book.bookId}.`);
        this.search(1);
      },
    );
  }

  protected loadAuthors(): void {
    this.authorLoading.set(true);
    this.authorError.set(null);
    this.books
      .authors()
      .pipe(finalize(() => this.authorLoading.set(false)))
      .subscribe({
        next: (authors) => {
          this.authors.set(authors);
          if (authors.length > 0) {
            this.createForm.controls.authorId.setValue(authors[0].authorId);
          } else {
            this.createForm.controls.authorMode.setValue('new');
          }
          this.createForm.updateValueAndValidity();
        },
        error: () => {
          this.authorError.set('Existing authors could not be loaded. You can still add a new author.');
          this.createForm.controls.authorMode.setValue('new');
          this.createForm.updateValueAndValidity();
        },
      });
  }

  protected getBook(): void {
    if (this.lookupForm.invalid) {
      this.lookupForm.markAllAsTouched();
      return;
    }

    this.runManagementRequest(this.books.get(this.lookupForm.getRawValue().bookId), (book) => {
      this.selectedBook.set(book);
      this.deletePending.set(false);
      this.managementMessage.set(`Book ${book.bookId} is ready to review.`);
    });
  }

  protected confirmDelete(): void {
    const book = this.selectedBook();
    if (!book) {
      return;
    }

    this.runManagementRequest(this.books.delete(book.bookId), () => {
      this.selectedBook.set(null);
      this.deletePending.set(false);
      this.managementMessage.set(`Book ${book.bookId} was deleted.`);
      this.search(1);
    });
  }

  protected managementFailure(error: { status?: number }): void {
    this.managementError.set(
      error.status === 404
        ? 'No book exists with that ID.'
        : error.status === 409
          ? 'The selected author no longer matches the catalogue. Reload and try again.'
          : error.status === 400
            ? 'Check the highlighted fields and try again.'
            : error.status === 401 || error.status === 403
              ? 'Your account cannot manage the catalogue.'
              : 'The catalogue could not complete that operation. Try again.',
    );
  }

  private runManagementRequest<T>(request: Observable<T>, success: (value: T) => void): void {
    this.managementLoading.set(true);
    this.managementError.set(null);
    this.managementMessage.set(null);
    request.pipe(finalize(() => this.managementLoading.set(false))).subscribe({
      next: success,
      error: (error: { status?: number }) => this.managementFailure(error),
    });
  }
}
