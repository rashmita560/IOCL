Imports System.Linq.Expressions
Imports Microsoft.EntityFrameworkCore
Imports IOCLCommunityHall.Data

Namespace Repositories
    Public Class GenericRepository(Of T As Class)
        Implements IGenericRepository(Of T)

        Protected ReadOnly _context As ApplicationDbContext
        Protected ReadOnly _dbSet As DbSet(Of T)

        Public Sub New(context As ApplicationDbContext)
            _context = context
            _dbSet = context.Set(Of T)()
        End Sub

        Public Async Function GetByIdAsync(id As Integer) As Task(Of T) Implements IGenericRepository(Of T).GetByIdAsync
            Return Await _dbSet.FindAsync(id)
        End Function

        Public Async Function GetAllAsync() As Task(Of IEnumerable(Of T)) Implements IGenericRepository(Of T).GetAllAsync
            Return Await _dbSet.ToListAsync()
        End Function

        Public Async Function FindAsync(predicate As Expression(Of Func(Of T, Boolean))) As Task(Of IEnumerable(Of T)) Implements IGenericRepository(Of T).FindAsync
            Return Await _dbSet.Where(predicate).ToListAsync()
        End Function

        Public Async Function AddAsync(entity As T) As Task Implements IGenericRepository(Of T).AddAsync
            Await _dbSet.AddAsync(entity)
        End Function

        Public Sub Update(entity As T) Implements IGenericRepository(Of T).Update
            _dbSet.Update(entity)
        End Sub

        Public Sub Delete(entity As T) Implements IGenericRepository(Of T).Delete
            _dbSet.Remove(entity)
        End Sub

        Public Async Function SaveAsync() As Task(Of Integer) Implements IGenericRepository(Of T).SaveAsync
            Return Await _context.SaveChangesAsync()
        End Function

        Public Async Function ExistsAsync(predicate As Expression(Of Func(Of T, Boolean))) As Task(Of Boolean) Implements IGenericRepository(Of T).ExistsAsync
            Return Await _dbSet.AnyAsync(predicate)
        End Function

        Public Async Function CountAsync(predicate As Expression(Of Func(Of T, Boolean))) As Task(Of Integer) Implements IGenericRepository(Of T).CountAsync
            Return Await _dbSet.CountAsync(predicate)
        End Function
    End Class
End Namespace
