using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.API.Data
{
    public class DatingRepository : IDatingRepository
    {
        public DataContext Context { get; set; }
        public DatingRepository(DataContext context)
        {
            this.Context = context;
        }
        public void Add<T>(T entity) where T : class
        {
            Context.Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            Context.Remove(entity);
        }

        public async Task<Photo> GetPhoto(int id) // id of Photo
        {
            var photo = await Context.Photos.FirstOrDefaultAsync( p => p.Id == id);
            return photo;
        }

        public async Task<User> GetUser(int id)
        {
            // Include is just 'join' in SQL. Every time we return a user, 
            // it must include the associated photos
            var user = await Context.Users.Include(p => p.Photos).FirstOrDefaultAsync(u => u.Id == id);
            return user;
        }

        public async Task<PagedList<User>> GetUsers(UserParams userParams)
        {
            var source = Context.Users.Include(p => p.Photos).OrderByDescending(u => u.LastActive).AsQueryable();

            source = source.Where(u => u.Id != userParams.UserId && u.Gender == userParams.Gender);

            if (userParams.Likers)
            {
                var userLikers = await GetUserLikes(userParams.UserId, userParams.Likers);
                source = source.Where(u => userLikers.Contains(u.Id));
            }

            if (userParams.Likees)
            {
                var userLikees = await GetUserLikes(userParams.UserId, userParams.Likers);
                source = source.Where(u => userLikees.Contains(u.Id));
            }

            if (userParams.MinAge != 18 || userParams.MaxAge != 99)
            {
                var minDob = DateTime.Today.AddYears(-userParams.MaxAge - 1);
                var maxDob = DateTime.Today.AddYears(-userParams.MinAge);
                source = source.Where(u => u.DateOfBirth >= minDob && u.DateOfBirth <= maxDob);
            }

            if (!string.IsNullOrEmpty(userParams.OrderBy))
            {
                switch(userParams.OrderBy)
                {
                    case "created":
                        source = source.OrderByDescending(u => u.Created);
                        break;
                    default:
                        source = source.OrderByDescending(u => u.LastActive);
                        break;
                }
            }

            var users = await PagedList<User>.CreateAsync(source, userParams.PageNumber, userParams.PageSize);
            
            return users;
        }

        public async Task<bool> SaveAll()
        {
            return await Context.SaveChangesAsync() > 0;
        }

        public async Task<Photo> GetMainPhotoForUser(int userId)
        {
            //return await Context.Photos.FirstOrDefaultAsync(p => p.UserId == userId && p.IsMain);
            return await Context.Photos.Where(p => p.UserId == userId).FirstOrDefaultAsync(p => p.IsMain);
        }

        private async Task<IEnumerable<int>> GetUserLikes(int id, bool likers)
        {
            var user = await this.Context.Users
                .Include(x => x.Likers) // populate the collection of Likers in User class
                .Include(x => x.Likees) // populate the collection of Likees in User class
                .FirstOrDefaultAsync(u => u.Id == id);
            
            if (likers)
            {   // returns a list of integers - i.e, IDs
                return user.Likers.Where(u => u.LikeeId == id).Select(i => i.LikerId);
            }
            else
            {
                return user.Likees.Where(u => u.LikerId == id).Select(i => i.LikeeId);
            }
        }

        public async Task<Like> GetLike(int userId, int recipientId)
        {
            return await Context.Likes.FirstOrDefaultAsync(u => u.LikerId == userId && u.LikeeId == recipientId);
        }
    }
}
