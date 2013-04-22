-- # -------------------------------------
-- # TEMPLATE Get

select
	TypeID,
	BaseTypeID,
	ClrType,
	Name,
	IsAbstract,
	TableName,
	AccountID,
	ChannelID
from MD_EdgeType
where
	AccountID in (-1, @accountID) and
	ChannelID in (-1, @channelID)
;

-- # -------------------------------------
-- # TEMPLATE Get/EdgeTypeFields

select
	types.TypeID as ParentTypeID,
	fields.FieldID as FieldID,
	fields.FieldType as FieldType,
	fields.FieldTypeID as FieldTypeID,
	fields.Name as Name,
	typeFields.ColumnName as ColumnName,
	typeFields.IsIdentity as IsIdentity
from
	MD_EdgeType as types
	inner join MD_EdgeTypeField as typeFields on
		typeFields.ParentTypeID in (-1, types.TypeID)
	inner join MD_EdgeField as fields on
		fields.FieldID = typeFields.FieldID and
		fields.AccountID in (-1, types.AccountID) and
		fields.ChannelID in (-1, types.ChannelID)

where
	types.AccountID in (-1, @accountID) and
	types.ChannelID in (-1, @channelID)
;

-- # -------------------------------------
-- # TEMPLATE Save

merge MD_EdgeType target
using
	(select
		@TypeID,
		@BaseTypeID,
		@ClrType,
		@Name,
		@IsAbstract,
		@TableName,
		@AccountID,
		@ChannelID
	)
	AS source
	(
		TypeID,
		BaseTypeID,
		ClrType,
		Name,
		IsAbstract,
		TableName,
		AccountID,
		ChannelID
	)
on
	target.TypeID = source.TypeID
when matched then
	update set
		BaseTypeID = source.BaseTypeID,
		ClrType = source.ClrType,
		Name = source.Name,
		IsAbstract = source.IsAbstract,
		TableName = source.TableName,
		AccountID = source.AccountID,
		ChannelID = source.ChannelID
when not matched then
	insert
	(
		BaseTypeID,
		ClrType,
		Name,
		IsAbstract,
		TableName,
		AccountID,
		ChannelID
	)
	values
	(
		source.BaseTypeID,
		source.ClrType,
		source.Name,
		source.IsAbstract,
		source.TableName,
		source.AccountID,
		source.ChannelID
	)
;

-- # -------------------------------------
-- # TEMPLATE Save/EdgeTypeFields

merge MD_EdgeTypeField target
using
	(select
		@ParentTypeID,
		@FieldID,
		@ColumnName,
		@IsIdentity
	)
	as source
	(
		ParentTypeID,
		FieldID,
		ColumnName,
		IsIdentity
	)
on
	target.ParentTypeID = source.ParentTypeID and
	target.FieldID = source.FieldID
when matched then
	update set
		ColumnName = source.ColumnName,
		IsIdentity = source.IsIdentity
when not matched by target then
	insert
	(
		ParentTypeID,
		FieldID,
		ColumnName,
		IsIdentity
	)
	values
	(
		source.ParentTypeID,
		source.FieldID,
		source.ColumnName,
		source.IsIdentity
	)
when not matched by source then
	delete
;