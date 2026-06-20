Imports Microsoft.AspNetCore.Identity
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.DependencyInjection
Imports IOCLCommunityHall.Models.Entities

Namespace Data
    Public Class SeedData
        Public Shared Async Function InitializeAsync(serviceProvider As IServiceProvider) As Task
            Dim context = serviceProvider.GetRequiredService(Of ApplicationDbContext)()
            Dim userManager = serviceProvider.GetRequiredService(Of UserManager(Of ApplicationUser))()
            Dim roleManager = serviceProvider.GetRequiredService(Of RoleManager(Of IdentityRole))()

            ' ── Roles ──────────────────────────────────────────────────────────────
            ' Hierarchy: SuperAdmin (HR) > GM > HOD > User (Employee)
            Dim roles = New String() {"SuperAdmin", "GM", "HOD", "User"}
            For Each roleName In roles
                If Not Await roleManager.RoleExistsAsync(roleName) Then
                    Await roleManager.CreateAsync(New IdentityRole(roleName))
                End If
            Next

            ' ── Employee Master Records ─────────────────────────────────────────────
            ' Must be created before ApplicationUser (FK dependency)
            If Not Await context.Employees.AnyAsync() Then
                context.Employees.AddRange(
                    New Employee With {
                        .EmployeeId = "00000001",
                        .EmployeeName = "Super Administrator",
                        .Department = "IT Administration",
                        .Designation = "System Administrator / HR",
                        .Email = "sysadmin@iocl.co.in",
                        .PhoneNumber = "0000000000",
                        .QuarterAddress = "IOCL Township Admin Block",
                        .Status = EmployeeStatus.Active
                    },
                    New Employee With {
                        .EmployeeId = "20000001",
                        .EmployeeName = "Suresh Patel",
                        .Department = "General Management",
                        .Designation = "General Manager",
                        .Email = "suresh.patel@iocl.co.in",
                        .PhoneNumber = "9800000001",
                        .QuarterAddress = "Block A, GM Quarter 01",
                        .Status = EmployeeStatus.Active
                    },
                    New Employee With {
                        .EmployeeId = "30000001",
                        .EmployeeName = "Arjun Mehta",
                        .Department = "Engineering & Projects",
                        .Designation = "Head of Department",
                        .Email = "arjun.mehta@iocl.co.in",
                        .PhoneNumber = "9800000002",
                        .QuarterAddress = "Block B, HOD Quarter 05",
                        .Status = EmployeeStatus.Active
                    },
                    New Employee With {
                        .EmployeeId = "10000001",
                        .EmployeeName = "Rajesh Sharma",
                        .Department = "Engineering & Projects",
                        .Designation = "Deputy Manager",
                        .Email = "rajesh.sharma@iocl.co.in",
                        .PhoneNumber = "9876543210",
                        .QuarterAddress = "Block A, Quarter 101",
                        .Status = EmployeeStatus.Active
                    },
                    New Employee With {
                        .EmployeeId = "12345678",
                        .EmployeeName = "Ramesh Kumar",
                        .Department = "Refinery Operations",
                        .Designation = "Senior Engineer",
                        .Email = "ramesh.kumar@iocl.co.in",
                        .PhoneNumber = "9876500001",
                        .QuarterAddress = "Block C, Quarter 305",
                        .Status = EmployeeStatus.Active
                    },
                    New Employee With {
                        .EmployeeId = "23456789",
                        .EmployeeName = "Priya Singh",
                        .Department = "Human Resources",
                        .Designation = "HR Executive",
                        .Email = "priya.singh@iocl.co.in",
                        .PhoneNumber = "9876500002",
                        .QuarterAddress = "Block B, Quarter 210",
                        .Status = EmployeeStatus.Active
                    }
                )
                Await context.SaveChangesAsync()
            End If

            ' ── Super Admin / HR Auth Account ───────────────────────────────────────
            ' Login: EmployeeId = 00000001, Password = Admin@1234
            Dim superAdmin = Await userManager.FindByNameAsync("00000001")
            If superAdmin Is Nothing Then
                superAdmin = New ApplicationUser With {
                    .UserName = "00000001",
                    .Email = "sysadmin@iocl.co.in",
                    .NormalizedEmail = "SYSADMIN@IOCL.CO.IN",
                    .FullName = "Super Administrator",
                    .EmployeeId = "00000001",
                    .Department = "IT Administration",
                    .EmailConfirmed = True,
                    .IsActive = True,
                    .MustChangePassword = False
                }
                Await userManager.CreateAsync(superAdmin, "Admin@1234")
                Await userManager.AddToRoleAsync(superAdmin, "SuperAdmin")
            End If

            ' ── General Manager (GM) Auth Account ──────────────────────────────────
            ' Login: EmployeeId = 20000001, Password = Gm@12345
            Dim gmUser = Await userManager.FindByNameAsync("20000001")
            If gmUser Is Nothing Then
                gmUser = New ApplicationUser With {
                    .UserName = "20000001",
                    .Email = "suresh.patel@iocl.co.in",
                    .NormalizedEmail = "SURESH.PATEL@IOCL.CO.IN",
                    .FullName = "Suresh Patel",
                    .EmployeeId = "20000001",
                    .Department = "General Management",
                    .EmailConfirmed = True,
                    .IsActive = True,
                    .MustChangePassword = False
                }
                Await userManager.CreateAsync(gmUser, "Gm@12345")
                Await userManager.AddToRoleAsync(gmUser, "GM")
            End If

            ' ── HOD Auth Account ────────────────────────────────────────────────────
            ' Login: EmployeeId = 30000001, Password = Hod@1234
            Dim hodUser = Await userManager.FindByNameAsync("30000001")
            If hodUser Is Nothing Then
                hodUser = New ApplicationUser With {
                    .UserName = "30000001",
                    .Email = "arjun.mehta@iocl.co.in",
                    .NormalizedEmail = "ARJUN.MEHTA@IOCL.CO.IN",
                    .FullName = "Arjun Mehta",
                    .EmployeeId = "30000001",
                    .Department = "Engineering & Projects",
                    .EmailConfirmed = True,
                    .IsActive = True,
                    .MustChangePassword = False
                }
                Await userManager.CreateAsync(hodUser, "Hod@1234")
                Await userManager.AddToRoleAsync(hodUser, "HOD")
            End If

            ' ── Employee (User) Auth Account 1 ──────────────────────────────────────
            ' Login: EmployeeId = 10000001, Password = Admin@1234
            Dim admin = Await userManager.FindByNameAsync("10000001")
            If admin Is Nothing Then
                admin = New ApplicationUser With {
                    .UserName = "10000001",
                    .Email = "rajesh.sharma@iocl.co.in",
                    .NormalizedEmail = "RAJESH.SHARMA@IOCL.CO.IN",
                    .FullName = "Rajesh Sharma",
                    .EmployeeId = "10000001",
                    .Department = "Engineering & Projects",
                    .EmailConfirmed = True,
                    .IsActive = True,
                    .MustChangePassword = False
                }
                Await userManager.CreateAsync(admin, "Admin@1234")
                Await userManager.AddToRoleAsync(admin, "User")
            End If

            ' ── Employee (User) Auth Account 2 ──────────────────────────────────────
            ' Login: EmployeeId = 12345678, Password = User@1234
            Dim user1 = Await userManager.FindByNameAsync("12345678")
            If user1 Is Nothing Then
                user1 = New ApplicationUser With {
                    .UserName = "12345678",
                    .Email = "ramesh.kumar@iocl.co.in",
                    .NormalizedEmail = "RAMESH.KUMAR@IOCL.CO.IN",
                    .FullName = "Ramesh Kumar",
                    .EmployeeId = "12345678",
                    .Department = "Refinery Operations",
                    .EmailConfirmed = True,
                    .IsActive = True,
                    .MustChangePassword = False
                }
                Await userManager.CreateAsync(user1, "User@1234")
                Await userManager.AddToRoleAsync(user1, "User")
            End If

            ' ── Employee (User) Auth Account 3 ──────────────────────────────────────
            ' Login: EmployeeId = 23456789, Password = User@1234
            Dim user2 = Await userManager.FindByNameAsync("23456789")
            If user2 Is Nothing Then
                user2 = New ApplicationUser With {
                    .UserName = "23456789",
                    .Email = "priya.singh@iocl.co.in",
                    .NormalizedEmail = "PRIYA.SINGH@IOCL.CO.IN",
                    .FullName = "Priya Singh",
                    .EmployeeId = "23456789",
                    .Department = "Human Resources",
                    .EmailConfirmed = True,
                    .IsActive = True,
                    .MustChangePassword = False
                }
                Await userManager.CreateAsync(user2, "User@1234")
                Await userManager.AddToRoleAsync(user2, "User")
            End If


            ' ── Inventory Categories ────────────────────────────────────────────────
            If Not Await context.InventoryCategories.AnyAsync() Then
                context.InventoryCategories.AddRange(
                    New InventoryCategory With {.Name = "Furniture", .Description = "Chairs, tables, sofas, etc."},
                    New InventoryCategory With {.Name = "Linen & Bedding", .Description = "Mattresses, bedsheets, blankets, pillows"},
                    New InventoryCategory With {.Name = "Utensils & Crockery", .Description = "Plates, cups, cooking vessels"},
                    New InventoryCategory With {.Name = "Electronics & AV", .Description = "Sound systems, mikes, projectors, lights"},
                    New InventoryCategory With {.Name = "Tents & Canopies", .Description = "Tents, shamiana, mandap material"},
                    New InventoryCategory With {.Name = "Decoration", .Description = "Flower pots, banners, stage decoration items"}
                )
                Await context.SaveChangesAsync()
            End If

            ' ── Inventory Items ─────────────────────────────────────────────────────
            If Not Await context.InventoryItems.AnyAsync() Then
                Dim furnitureCat = Await context.InventoryCategories.FirstAsync(Function(c) c.Name = "Furniture")
                Dim linenCat = Await context.InventoryCategories.FirstAsync(Function(c) c.Name = "Linen & Bedding")
                Dim utensilsCat = Await context.InventoryCategories.FirstAsync(Function(c) c.Name = "Utensils & Crockery")
                Dim electronicsCat = Await context.InventoryCategories.FirstAsync(Function(c) c.Name = "Electronics & AV")
                Dim tentsCat = Await context.InventoryCategories.FirstAsync(Function(c) c.Name = "Tents & Canopies")
                Dim decorCat = Await context.InventoryCategories.FirstAsync(Function(c) c.Name = "Decoration")

                context.InventoryItems.AddRange(
                    New InventoryItem With {.Name = "Plastic Chair", .CategoryId = furnitureCat.Id, .TotalQuantity = 500, .CurrentPrice = 5, .UnitType = "Nos", .Description = "White plastic chairs for events"},
                    New InventoryItem With {.Name = "Folding Table (6 ft)", .CategoryId = furnitureCat.Id, .TotalQuantity = 100, .CurrentPrice = 50, .UnitType = "Nos", .Description = "6-foot folding tables"},
                    New InventoryItem With {.Name = "Stage Chair (Cushioned)", .CategoryId = furnitureCat.Id, .TotalQuantity = 50, .CurrentPrice = 20, .UnitType = "Nos", .Description = "Cushioned chairs for stage/VIP"},
                    New InventoryItem With {.Name = "Single Mattress", .CategoryId = linenCat.Id, .TotalQuantity = 100, .CurrentPrice = 30, .UnitType = "Nos", .Description = "Single mattresses for overnight stays"},
                    New InventoryItem With {.Name = "Bedsheet (Single)", .CategoryId = linenCat.Id, .TotalQuantity = 200, .CurrentPrice = 10, .UnitType = "Nos", .Description = "Single bedsheets"},
                    New InventoryItem With {.Name = "Blanket", .CategoryId = linenCat.Id, .TotalQuantity = 100, .CurrentPrice = 15, .UnitType = "Nos", .Description = "Woollen blankets"},
                    New InventoryItem With {.Name = "Steel Plate (Thali)", .CategoryId = utensilsCat.Id, .TotalQuantity = 300, .CurrentPrice = 3, .UnitType = "Nos", .Description = "Steel dinner plates"},
                    New InventoryItem With {.Name = "Steel Glass", .CategoryId = utensilsCat.Id, .TotalQuantity = 300, .CurrentPrice = 2, .UnitType = "Nos", .Description = "Steel drinking glasses"},
                    New InventoryItem With {.Name = "Big Cooking Vessel (Patila)", .CategoryId = utensilsCat.Id, .TotalQuantity = 20, .CurrentPrice = 100, .UnitType = "Nos", .Description = "Large cooking vessels"},
                    New InventoryItem With {.Name = "Sound System (PA Set)", .CategoryId = electronicsCat.Id, .TotalQuantity = 5, .CurrentPrice = 500, .UnitType = "Set", .Description = "Full PA sound system with speakers and mixer"},
                    New InventoryItem With {.Name = "Wireless Mike Set", .CategoryId = electronicsCat.Id, .TotalQuantity = 10, .CurrentPrice = 200, .UnitType = "Set", .Description = "2-piece wireless microphone sets"},
                    New InventoryItem With {.Name = "LED Light String (10m)", .CategoryId = electronicsCat.Id, .TotalQuantity = 50, .CurrentPrice = 50, .UnitType = "Nos", .Description = "Decorative LED light strings"},
                    New InventoryItem With {.Name = "Projector (Full HD)", .CategoryId = electronicsCat.Id, .TotalQuantity = 3, .CurrentPrice = 800, .UnitType = "Nos", .Description = "Full HD projector with screen"},
                    New InventoryItem With {.Name = "Shamiana (Small, 20x20ft)", .CategoryId = tentsCat.Id, .TotalQuantity = 10, .CurrentPrice = 1000, .UnitType = "Nos", .Description = "Small shamiana tent 20x20 feet"},
                    New InventoryItem With {.Name = "Shamiana (Large, 40x60ft)", .CategoryId = tentsCat.Id, .TotalQuantity = 5, .CurrentPrice = 3000, .UnitType = "Nos", .Description = "Large shamiana tent 40x60 feet"},
                    New InventoryItem With {.Name = "Stage Platform (4x4ft section)", .CategoryId = decorCat.Id, .TotalQuantity = 30, .CurrentPrice = 150, .UnitType = "Section", .Description = "Modular stage platform sections"},
                    New InventoryItem With {.Name = "Flower Pot (Decorative)", .CategoryId = decorCat.Id, .TotalQuantity = 40, .CurrentPrice = 25, .UnitType = "Nos", .Description = "Decorative flower pots for stage/entrance"}
                )
            End If

            Await context.SaveChangesAsync()
        End Function
    End Class
End Namespace
