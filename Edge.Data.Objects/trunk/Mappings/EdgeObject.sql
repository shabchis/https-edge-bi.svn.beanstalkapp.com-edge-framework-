-- # -------------------------------------
-- # TEMPLATE EdgeObject.GetByGK.Root

select
	-- #COLUMNS-START
	GK as GK,
	AccountID as AccountID
	-- #COLUMNS-END
from
	EdgeObject
where
	GK = @gk;

-- # -------------------------------------
-- # TEMPLATE EdgeObject.GetByGK.Connections

select * from
(
	select
		ConnectionDefID,
		ToObjectType,
		ToObjectGK
	from
		Connection
	where
		FromObjectType = @objectType and
		FromObjectGK = @objectGK
) as Connections
-- #FILTER
-- #SORTING
