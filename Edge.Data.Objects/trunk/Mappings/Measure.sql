-- # -------------------------------------
-- # TEMPLATE GetInstances

select * from
(
	select
		ISNULL(b_ID, a_ID) as ID,
		ISNULL(b_AccountID, a_AccountID) as AccountID,
		ISNULL(b_Name, a_Name) as Name,
		ISNULL(b_DataType, a_DataType) as DataType,
		ISNULL(b_DisplayName, a_DisplayName) as DisplayName,
		ISNULL(b_StringFormat, a_StringFormat) as StringFormat,
		CASE bitwiseOr WHEN 1 THEN ISNULL(a_Options,0) | ISNULL(b_Options,0) ELSE ISNULL(b_Options, ISNULL(a_Options,0)) END as Options,
		CASE when a_ID is not null and b_ID is not null then CAST(1 as bit) else CAST(0 as bit) END as IsInstance
	from
	(
		select
			a.ID as a_ID, a.AccountID as a_AccountID, a.Name as a_Name, a.DataType as a_DataType, a.DisplayName as a_DisplayName, a.StringFormat as a_StringFormat, a.Options as a_Options, ISNULL(a.OptionsOverride,1) as bitwiseOr,
			b.ID as b_ID, b.AccountID as b_AccountID, b.Name as b_Name, b.DataType as b_DataType, b.DisplayName as b_DisplayName, b.StringFormat as b_StringFormat, b.Options as b_Options
		from
			MD_Measure a
			left outer join MD_Measure b on
				b.AccountID = @accountID and b.Name = a.Name
		where
			a.AccountID = -1

		union all

		select
			a.ID as a_ID, a.AccountID as a_AccountID, a.Name as a_Name, a.DataType as a_DataType, a.DisplayName as a_DisplayName, a.StringFormat as a_StringFormat, a.Options as a_Options, 0 as bitwiseOr,
			b.ID as b_ID, b.AccountID as b_AccountID, b.Name as b_Name, b.DataType as b_DataType, b.DisplayName as b_DisplayName, b.StringFormat as b_StringFormat, b.Options as b_Options
		from
			MD_Measure b
			left outer join MD_Measure a on
				a.AccountID = -1 and a.Name = b.Name
		where
			b.AccountID = @accountID and a.ID is null
	) as joined
) as instances

where
	(@operator = 1 and @flags & Options >= @flags) or
	(@operator = 0 and @flags & Options > 0) or
	(@operator = -1 and @flags & Options = 0)
;

