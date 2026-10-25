import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Author {
  authorId: number;
  name: string;
}

export interface Book {
  bookId: number;
  author: Author;
  title: string;
  subTitle: string | null;
}

export interface AuthorInput {
  authorId: number | null;
  name: string;
}

export interface SaveBook {
  author: AuthorInput;
  title: string;
  subTitle: string | null;
}

export interface PagedResult {
  items: Book[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class BookService {
  private readonly http = inject(HttpClient);

  authors(): Observable<Author[]> {
    return this.http.get<Author[]>(`${environment.apiUrl}/api/ui/authors`);
  }

  search(title: string, author: string, page: number): Observable<PagedResult> {
    let params = new HttpParams().set('page', page).set('pageSize', 20);
    if (title.trim()) {
      params = params.set('title', title.trim());
    }
    if (author.trim()) {
      params = params.set('author', author.trim());
    }

    return this.http.get<PagedResult>(`${environment.apiUrl}/api/books/search`, { params });
  }

  create(book: SaveBook): Observable<Book> {
    return this.http.post<Book>(`${environment.apiUrl}/api/ui/books`, book);
  }

  get(bookId: number): Observable<Book> {
    return this.http.get<Book>(`${environment.apiUrl}/api/ui/books/${bookId}`);
  }

  delete(bookId: number): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/api/ui/books/${bookId}`);
  }
}
