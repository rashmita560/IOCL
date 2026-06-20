Imports System.Linq.Expressions

Namespace Repositories
    Public Interface IGenericRepository(Of T As Class)
        Function GetByIdAsync(id As Integer) As Task(Of T)
        Function GetAllAsync() As Task(Of IEnumerable(Of T))
        Function FindAsync(predicate As Expression(Of Func(Of T, Boolean))) As Task(Of IEnumerable(Of T))
        Function AddAsync(entity As T) As Task
        Sub Update(entity As T)
        Sub Delete(entity As T)
        Function SaveAsync() As Task(Of Integer)
        Function ExistsAsync(predicate As Expression(Of Func(Of T, Boolean))) As Task(Of Boolean)
        Function CountAsync(predicate As Expression(Of Func(Of T, Boolean))) As Task(Of Integer)
    End Interface
End Namespace
