﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TheBlog.Data;
using TheBlog.Enums;
using TheBlog.Models;
using TheBlog.Services;
using X.PagedList;

namespace TheBlog.Controllers
{
    public class BlogsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IImageService _imageService;
        private readonly UserManager<BlogUser> _userManager;
        private readonly BlogSearchService _blogSearchService;

        public BlogsController(ApplicationDbContext context, IImageService imageService,
            UserManager<BlogUser> userManager, BlogSearchService blogSearchService)
        {
            _context = context;
            _imageService = imageService;
            _userManager = userManager;
            _blogSearchService = blogSearchService;
        }

        // GET: Blogs
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Blogs.Include(b => b.BlogUser);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Posts by Category
        public async Task<IActionResult> CategoryIndex(int? page, string categoryName)
        {
            ViewData["CategoryName"] = categoryName; // when we go from page 1 to page 2, we will maintain tagText

            var pageNumber = page ?? 1;
            var pageSize = 5;

            var posts = await _context.Posts
                .Include(p => p.BlogUser)
                .Where(p => p.Blog.Name.ToLower() == categoryName && p.ReadyStatus == ReadyStatus.ProductionReady)
                .OrderByDescending(p => p.Created)
                .ToPagedListAsync(pageNumber, pageSize);

            if (posts == null)
            {
                return NotFound();
            }

            ViewData["DistinctTags"] = await _blogSearchService.GetDistinctTags(15);
            ViewData["Categories"] = await _blogSearchService.GetDistinctCategories();

            return View(posts);
        }

        // GET: Blogs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var blog = await _context.Blogs
                .Include(b => b.BlogUser)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (blog == null)
            {
                return NotFound();
            }

            return View(blog);
        }

        // GET: Blogs/Create
        [Authorize(Roles = "Administrator, Moderator")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Blogs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Image")] Blog blog)
        {
            if (ModelState.IsValid)
            {
                blog.Created = DateTime.UtcNow;
                blog.BlogUserId = _userManager.GetUserId(User);

                blog.ImageData = await _imageService.EncodeImageAsync(blog.Image); //IFormFile to byte[]
                blog.ContentType = _imageService.ContentType(blog.Image);

                _context.Add(blog);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewData["BlogUserId"] = new SelectList(_context.Users, "Id", "Id", blog.BlogUserId);
            return View(blog);
        }

        // GET: Blogs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null)
            {
                return NotFound();
            }

            return View(blog);
        }

        // POST: Blogs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description")] Blog blog, IFormFile newImage)
        {
            if (id != blog.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // alt way
                    // var newBlog = await _context.Blogs.FindAsync(blog.Id);
                    //
                    // newBlog.Updated = DateTime.UtcNow;
                    //
                    // if (newBlog.Name != blog.Name)
                    // {
                    //     newBlog.Name = blog.Name;
                    // }
                    //
                    // if (newBlog.Description != blog.Description)
                    // {
                    //     newBlog.Description = blog.Description;
                    // }
                    //
                    // if (newImage is not null)
                    // {
                    //     newBlog.ImageData = await _imageService.EncodeImageAsync(newImage);
                    // }

                    var currentDbBlog = await _context.Blogs.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
                    blog.Updated = DateTime.UtcNow;
                    blog.BlogUserId = _userManager.GetUserId(User);
                    blog.Created = currentDbBlog.Created;

                    if (newImage is not null)
                    {
                        blog.ImageData = await _imageService.EncodeImageAsync(newImage);
                        blog.ContentType = newImage.ContentType;
                    }
                    else
                    {
                        blog.ImageData = currentDbBlog.ImageData;
                        blog.ContentType = currentDbBlog.ContentType;
                    }

                    _context.Update(blog);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BlogExists(blog.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["BlogUserId"] = new SelectList(_context.Users, "Id", "Id", blog.BlogUserId);
            return View(blog);
        }

        // GET: Blogs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var blog = await _context.Blogs
                .Include(b => b.BlogUser)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (blog == null)
            {
                return NotFound();
            }

            return View(blog);
        }

        // POST: Blogs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var blog = await _context.Blogs.FindAsync(id);
            _context.Blogs.Remove(blog);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BlogExists(int id)
        {
            return _context.Blogs.Any(e => e.Id == id);
        }
    }
}