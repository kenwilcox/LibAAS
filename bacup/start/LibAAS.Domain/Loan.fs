﻿module internal LibASS.Domain.Loan
open LibASS.Contracts
open LibASS.Domain.Types
open System

let handleAtInit stateGetters ((aggId:AggregateId), commandData) = 
    match commandData with
    | LoanItem (loanId, userId, itemId, libraryId) -> 
        stateGetters.GetInventoryItem itemId
        >>=
            fun item ->
                match item with
                | ItemInit -> InvalidItem |> fail
                | _ ->
                    let loan = 
                        { LoanId = loanId
                          UserId = userId
                          ItemId = itemId
                          LibraryId = libraryId }
                    let now = DateTime.Today
                    [ItemLoaned (loan, LoanDate now, DueDate (now.AddDays(7.)))] |> ok
    | _ -> InvalidState "Loan at init" |> fail

let handleAtCreated data ((aggId:AggregateId), commandData) =
    match commandData with
    | ReturnItem cData -> 
        let now = DateTime.Today
        let (DueDate duedate) = data.DueDate
        let daysLate = (now - duedate).Days
        let fine = 100 * daysLate
        if now > duedate then 
            [ItemLate (data.Loan, ReturnDate now, daysLate, Fine fine )] |> ok
        else 
            [ItemReturned (data.Loan, ReturnDate now )] |> ok
    | _ -> InvalidState "Loan at created" |> fail

let executeCommand  state stateGetters command =
    match state with
    | LoanInit -> handleAtInit stateGetters command
    | LoanCreated data -> command |> handleAtCreated data
    | _ -> InvalidState "Loan" |> fail

let evolveAtInit = function
    | ItemLoaned (loan, loanDate, dueDate) -> 
        LoanCreated {Loan = loan; DueDate = dueDate; LoanDate = loanDate} |> ok
    | _ -> InvalidStateTransition "Loan at init" |> fail

let evolveAtCreated data = function
    | ItemReturned (loan, returnDate) -> Returned (data, returnDate) |> ok
    | ItemLate (loan, returnDate, daysLate, fine) -> LateReturn (data, fine, returnDate) |> ok
    | _ -> InvalidState "Loan at created" |> fail

let evolveOne (event:EventData) state = 
    match state with
    | LoanInit -> evolveAtInit event
    | LoanCreated data -> event |> evolveAtCreated data
    | _ -> InvalidStateTransition "Loan" |> fail

let evolveSeed = {Init = LoanInit; EvolveOne = evolveOne}