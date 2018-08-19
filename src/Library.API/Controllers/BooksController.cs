using Library.API.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using AutoMapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Library.API.Models;
using Library.API.Entities;
using Library.API.Helpers;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;

namespace Library.API.Controllers
{
    [Route("api/authors/{authorId}/books")]
    public class BooksController : Controller
    {
        private ILogger<BooksController> _logger;
        private ILibraryRepository _libraryRepository;
        private IUrlHelper _urlHelper;

        public BooksController(ILibraryRepository libraryRepository, ILogger<BooksController> logger, IUrlHelper urlHelper)
        {
            _logger = logger;
            _libraryRepository = libraryRepository;
            _urlHelper = urlHelper;
        }

        [HttpGet(Name ="GetBooksForAuthor")]
        public IActionResult GetBooksForAuthor(Guid authorId)
        {
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }
            var booksForAuthorFromRepo = _libraryRepository.GetBooksForAuthor(authorId);
            var booksForAuthor = Mapper.Map<IEnumerable<BookDto>>(booksForAuthorFromRepo);

            booksForAuthor = booksForAuthor.Select(book =>
            {
                book = CreateLinksForBook(book);
                return book;
            });

            var wrapper = CreateLinksForBooks(new LinkedCollectionWrapperDto<BookDto>(booksForAuthor));


            return Ok(wrapper);

        }

        [HttpGet("{id}", Name = "GetBookForAuthor")]
        public IActionResult GetBookForAuthor(Guid authorId, Guid id)
        {
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if( bookForAuthorFromRepo == null)
            {
                return NotFound();
            }
            var bookForAuthor = Mapper.Map<BookDto>(bookForAuthorFromRepo);

            return Ok(CreateLinksForBook(bookForAuthor));

        }

        [HttpPost(Name ="CreateBookForAuthor")]
        public IActionResult CreateBookForAuthor(Guid authorId, [FromBody] BookForCreationDto book)
        {
            if (book == null)
            {
                return BadRequest();
            }
            if (book.Description == book.Title)
            {
                ModelState.AddModelError(nameof(BookForCreationDto),
                    "The provided description should be different from the title");
            }

            if(!ModelState.IsValid)
            {
                return new UnProcessableEntityObjectResult(ModelState);
            }

            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var bookEntity = Mapper.Map<Book>(book);
            _libraryRepository.AddBookForAuthor(authorId, bookEntity);

            if(!_libraryRepository.Save())
            {
                throw new Exception($"Creating a book for author {authorId} failed on save.");
            }

            var bookToReturn = Mapper.Map<BookDto>(bookEntity);
            return CreatedAtRoute("GetBookForAuthor", new { authorId = authorId, id = bookToReturn.Id }, CreateLinksForBook(bookToReturn));
        }

        [HttpDelete("{id}", Name = "DeleteBookForAuthor")]
        public IActionResult DeleteBookForAuthor( Guid authorId, Guid id)
        {
            if(!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }
            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if (bookForAuthorFromRepo == null)
            {
                return NotFound();
            }

            _libraryRepository.DeleteBook(bookForAuthorFromRepo);
            if (!_libraryRepository.Save())
            {
                throw new Exception($"Deleting book {id} for author {authorId} failed on save.");
            }
            _logger.LogInformation(100, $"Deleted book {id} for author {authorId}");

            return NoContent();

        }

        [HttpPut("{id}", Name = "UpdateBookForAuthor")]
        public IActionResult UpdateBookForAuthor(Guid authorId, Guid id, [FromBody] BookForUpdateDto book)
        {
            if (book == null)
            {
                return BadRequest();
            }
            if (book.Description == book.Title)
            {
                ModelState.AddModelError(nameof(BookForUpdateDto),
                    "The provided description should be different from the title");
            }

            if (!ModelState.IsValid)
            {
                return new UnProcessableEntityObjectResult(ModelState);
            }

            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }
            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if (bookForAuthorFromRepo == null)
            {
                var bookToAdd = Mapper.Map<Book>(book);
                bookToAdd.Id = id;

                _libraryRepository.AddBookForAuthor(authorId, bookToAdd);

                if (!_libraryRepository.Save())
                {
                    throw new Exception($"Upserting book {id} for author {authorId} failed on save.");
                }
                var bookToReturn = Mapper.Map<BookDto>(bookToAdd);
                return CreatedAtRoute("GetBookForAuthor", new { authorId = authorId, id = bookToReturn.Id }, bookToReturn); 
            }

            Mapper.Map(book, bookForAuthorFromRepo);

            _libraryRepository.UpdateBookForAuthor(bookForAuthorFromRepo);

            if (!_libraryRepository.Save())
            {
                throw new Exception($"Updating book {id} for author {authorId} failed on save.");
            }

            return NoContent();
        }

        [HttpPatch("{id}", Name = "PartiallyUpdateBookForAuthor")]
        public IActionResult PartiallyUpdateBookForAuthor( Guid authorId, Guid id,
            [FromBody] JsonPatchDocument<BookForUpdateDto> patchDoc )
        {
            if (patchDoc == null)
            {
                return BadRequest();
            }

            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }
            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if (bookForAuthorFromRepo == null)
            {
                var bookDto = new BookForUpdateDto();
                patchDoc.ApplyTo(bookDto, ModelState);
                if (bookDto.Description == bookDto.Title)
                {
                    ModelState.AddModelError(nameof(BookForUpdateDto),
                        "The provided description should be different from the title");
                }
                TryValidateModel(bookDto);

                if (!ModelState.IsValid)
                {
                    return new UnProcessableEntityObjectResult(ModelState);
                }


                var bookToAdd = Mapper.Map<Book>(bookDto);
                bookToAdd.Id = id;

                _libraryRepository.AddBookForAuthor(authorId, bookToAdd);

                if (!_libraryRepository.Save())
                {
                    throw new Exception($"Upserting book {id} for author {authorId} failed on save.");
                }

                var bookToReturn = Mapper.Map<BookDto>(bookToAdd);
                return CreatedAtRoute("GetBookForAuthor", new { authorId = authorId, id = bookToReturn.Id }, bookToReturn);


            }

            var bookToPatch = Mapper.Map<BookForUpdateDto>(bookForAuthorFromRepo);

            //patchDoc.ApplyTo(bookToPatch, ModelState);
            patchDoc.ApplyTo(bookToPatch);
            
            if (bookToPatch.Description == bookToPatch.Title)
            {
                ModelState.AddModelError(nameof(BookForUpdateDto),
                    "The provided description should be different from the title");
            }
            TryValidateModel(bookToPatch);

            if (!ModelState.IsValid)
            {
                return new UnProcessableEntityObjectResult(ModelState);
            }


            // add validation

            Mapper.Map(bookToPatch, bookForAuthorFromRepo);

            _libraryRepository.UpdateBookForAuthor(bookForAuthorFromRepo);

            if (!_libraryRepository.Save())
            {
                throw new Exception($"Patching book {id} for author {authorId} failed on save.");
            }

            return NoContent();
        }

        private BookDto CreateLinksForBook(BookDto book)
        {
            book.Links.Add(new LinkDto(_urlHelper.Link("GetBookForAuthor",
               new { id = book.Id }),
               "self",
               "GET"));

            book.Links.Add(
                new LinkDto(_urlHelper.Link("DeleteBookForAuthor",
                new { id = book.Id }),
                "delete_book",
                "DELETE"));

            book.Links.Add(
                new LinkDto(_urlHelper.Link("UpdateBookForAuthor",
                new { id = book.Id }),
                "update_book",
                "PUT"));

            book.Links.Add(
                new LinkDto(_urlHelper.Link("PartiallyUpdateBookForAuthor",
                new { id = book.Id }),
                "partially_update_book",
                "PATCH"));

            return book;
        }

        private LinkedCollectionWrapperDto<BookDto> CreateLinksForBooks(
            LinkedCollectionWrapperDto<BookDto> booksWrapper)
        {
            // link to self
            booksWrapper.Links.Add(
                new LinkDto(_urlHelper.Link("GetBooksForAuthor", new { }),
                "self",
                "GET"));

            return booksWrapper;
        }

    }
}
