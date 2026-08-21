using Intranet.Models;
using Intranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Intranet.Pages.Procurement
{
    [Authorize(Roles = "Admin,MD")]
    public class AdminModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly PasswordService _passwordService;

        public AdminModel(AppDbContext context, PasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        // ================= DATA =================
        public string UserFullName { get; set; } = string.Empty;

        public List<User> Users { get; set; } = new();
        public List<Role> Roles { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<Request> Requests { get; set; } = new();

        // ================= FILTERS =================
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DepartmentFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? RoleFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RequestSearch { get; set; }

        // ================= CREATE USER =================
        [BindProperty] public string Email { get; set; } = string.Empty;
        [BindProperty] public string FirstName { get; set; } = string.Empty;
        [BindProperty] public string Surname { get; set; } = string.Empty;
        [BindProperty] public int DepartmentId { get; set; }
        [BindProperty] public int RoleId { get; set; }

        // ================= RESET PASSWORD =================
        [BindProperty] public Guid ResetUserId { get; set; }
        [BindProperty] public string TempPassword { get; set; } = string.Empty;

        // ================= CREATE ROLE =================
        [BindProperty] public string NewRoleName { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var currentUser = await _context.Users.FindAsync(userId);

            UserFullName = currentUser != null
              ? $"{currentUser.FirstName} {currentUser.Surname}"
              : "User";

            // ================= USERS QUERY =================
            var usersQuery = _context.Users
        .Include(u => u.UserRoles)
        .ThenInclude(ur => ur.Role)
        .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                usersQuery = usersQuery.Where(u =>
                  u.Id.ToString().Contains(SearchTerm) ||
                  u.FirstName.Contains(SearchTerm) ||
                  u.Surname.Contains(SearchTerm) ||
                  u.Email.Contains(SearchTerm));
            }

            if (DepartmentFilter.HasValue)
            {
                usersQuery = usersQuery.Where(u =>
                  u.DepartmentId == DepartmentFilter.Value);
            }

            if (RoleFilter.HasValue)
            {
                usersQuery = usersQuery.Where(u =>
                  u.UserRoles.Any(r => r.RoleId == RoleFilter.Value));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                bool isActive = StatusFilter == "true";
                usersQuery = usersQuery.Where(u => u.IsActive == isActive);
            }

            Users = await usersQuery.ToListAsync();

            // ================= LOOKUPS =================
            Roles = await _context.Roles.ToListAsync();
            Departments = await _context.Departments.ToListAsync();

            // ================= REQUESTS QUERY =================
            var requestQuery = _context.Requests
        .Include(r => r.Requester)
        .AsQueryable();

            if (!string.IsNullOrWhiteSpace(RequestSearch))
            {
                requestQuery = requestQuery.Where(r =>
                  r.Description.Contains(RequestSearch));
            }

            Requests = await requestQuery
              .OrderByDescending(r => r.CreatedAt)
              .Take(50)
              .ToListAsync();
        }

        // ================= CREATE USER =================
        public async Task<IActionResult> OnPostCreateUserAsync()
        {
            if (RoleId <= 0)
            {
                TempData["Error"] = "Please select a role.";
                return RedirectToPage();
            }

            var role = await _context.Roles.FindAsync(RoleId);

            if (role == null)
            {
                TempData["Error"] = "Invalid role.";
                return RedirectToPage();
            }

            var tempPassword = "Temp@123";

            var user = new User
            {
                Email = Email,
                FirstName = FirstName,
                Surname = Surname,
                DepartmentId = DepartmentId,
                IsActive = true,
                PasswordHash = _passwordService.HashPassword(tempPassword),
                MustChangePassword = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = RoleId
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"User created. Temp password: {tempPassword}";
            return RedirectToPage();
        }

        // ================= RESET PASSWORD =================
        public async Task<IActionResult> OnPostResetPasswordAsync()
        {
            var user = await _context.Users.FindAsync(ResetUserId);

            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(TempPassword))
            {
                TempData["Error"] = "Temporary password cannot be empty.";
                return RedirectToPage();
            }

            user.PasswordHash = _passwordService.HashPassword(TempPassword);
            user.MustChangePassword = true;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Password reset successfully.";
            return RedirectToPage();
        }

        // ================= UPDATE USER =================
        public async Task<IActionResult> OnPostUpdateUserAsync(
      Guid id,
      string email,
      string firstName,
      string surname,
      List<int> roleIds,
      bool isActive,
      int departmentId)
        {
            var user = await _context.Users
              .Include(u => u.UserRoles)
              .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToPage();
            }

            user.Email = email;
            user.FirstName = firstName;
            user.Surname = surname;
            user.IsActive = isActive;
            user.DepartmentId = departmentId;

            user.UserRoles.Clear();

            if (roleIds != null && roleIds.Any())
            {
                foreach (var roleId in roleIds.Distinct())
                {
                    if (roleId > 0)
                    {
                        user.UserRoles.Add(new UserRole
                        {
                            UserId = user.Id,
                            RoleId = roleId
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "User updated successfully.";
            return RedirectToPage();
        }

        // ================= CREATE ROLE =================
        public async Task<IActionResult> OnPostCreateRoleAsync()
        {
            if (string.IsNullOrWhiteSpace(NewRoleName))
            {
                TempData["Error"] = "Role name required.";
                return RedirectToPage();
            }

            _context.Roles.Add(new Role
            {
                RoleName = NewRoleName.Trim()
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Role created successfully.";
            return RedirectToPage();
        }

        // ================= CANCEL REQUEST =================
        public async Task<IActionResult> OnPostCancelRequestAsync(int id)
        {
            var request = await _context.Requests.FindAsync(id);

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToPage();
            }

            request.Status = "Cancelled";
            request.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Request cancelled.";
            return RedirectToPage();
        }
    }
}