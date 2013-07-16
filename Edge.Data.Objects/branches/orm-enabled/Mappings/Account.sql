-- # -------------------------------------
-- # TEMPLATE Get

select *
from Account 
where @accountID = -1 or ID = @accountID or ParentAccountID = @accountID;


-- # -------------------------------------
-- # TEMPLATE Save

merge Account target
using (select @ID, @Name, @ParentAccountID, @Status) AS source (ID, Name, ParentAccountID, Status)
on target.ID = source.ID
when matched then
	update set Name = source.Name, ParentAccountID = source.ParentAccountID, Status = source.Status
when not matched then
	insert values(source.ID, source.Name, source.ParentAccountID, source.Status)
;