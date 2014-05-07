using System.Data.Entity;

using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace Filemanager.MVC.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        static ApplicationDbContext()
        {
            Database.SetInitializer(new MyDbInitializer());
        }

        public ApplicationDbContext()
            : base("DefaultConnection")
        {
        }
    }

    public class MyDbInitializer : DropCreateDatabaseIfModelChanges<ApplicationDbContext>
    {
        protected override void Seed(ApplicationDbContext context)
        {
            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context));
            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));

            const string AdminRole = "Admin";

            const string AdminUsername = "Administrator";
            const string AdminPassword = "filemanager";

            if (!roleManager.RoleExists(AdminRole))
            {
                roleManager.Create(new IdentityRole(AdminRole));
            }

            ApplicationUser user = userManager.FindByName(AdminUsername);

            if (user == null)
            {
                user = new ApplicationUser { UserName = AdminUsername };
                userManager.Create(user, AdminPassword);
            }

            if (!userManager.IsInRole(user.Id, AdminRole))
            {
                userManager.AddToRole(user.Id, AdminRole);
            }

            base.Seed(context);
        }
    }
}