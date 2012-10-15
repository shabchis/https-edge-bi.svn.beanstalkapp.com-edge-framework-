-- # -------------------------------------
-- # TEMPLATE Measure.Queries.Get

select *
from
(
	select
		-- # COLUMNS-BEGIN
		-- ----------------------
			this.ID												as ID				-- # COLUMN
			,isnull(this.Name,			base.Name)				as Name				-- # COLUMN
			,isnull(this.DisplayName,	base.DisplayName)		as DisplayName		-- # COLUMN
			,isnull(this.AccountID,		base.AccountID)			as AccountID		-- # COLUMN
			,isnull(this.ChannelID,		base.ChannelID)			as ChannelID		-- # COLUMN
			,isnull(this.StringFormat,	base.StringFormat)		as StringFormat		-- # COLUMN
			,base.DataType										as DataType			-- # COLUMN
			,base.Options										as Options			-- # COLUMN
		-- ----------------------
		-- # COLUMNS-END
	from
		Measure this
		left outer join Measure base on 
			base.AccountID = -1 and
			base.MeasureID <> this.MeasureID and
			base.MeasureID = this.BaseMeasureID
	where
		(
			-- if a specific measure is specified, get it
			@measureID is null or this.MeasureID = @measureID
		)
		and
		(
			-- if a specific channel is specified, get it
			@channelID is null or this.ChannelID = -1 or this.ChannelID = @channelID 
		)
		and
		(
			(
				-- if account is global, get all globals
				@accountID = -1 and this.AccountID = -1
			)
			or
			(
				-- else if its not global:
				@accountID <> -1 and
				(
					(
						-- get either measures assigned to this account...
						this.AccountID = @accountID
					)
					or
					(
						-- or, global measures that are not BO and that are not overriden by this account
						this.AccountID = -1 and
						(
							@includeBase = 1 or
							this.MeasureID not in
							(
								select distinct BaseMeasureID
								from Measure
								where AccountID = @accountID
							)
						)
					)
				)
			)
		)
) as m

-- # WHERE

-- # SORTING