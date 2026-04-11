export type LogSegment =
  | { type: 'text'; text: string; color?: string }
  | { type: 'image'; src: string }

export function parseLogSegments(message: string): LogSegment[] {
  const segments: LogSegment[] = []
  let remaining = message

  while (remaining.length > 0) {
    // [img:filename] token
    const imgMatch = /^\[img:([^\]]+)\]/.exec(remaining)
    if (imgMatch) {
      segments.push({ type: 'image', src: 'images/' + imgMatch[1] })
      remaining = remaining.slice(imgMatch[0].length)
      continue
    }

    // &rrggbb<text> color token
    const colorMatch = /^&([0-9a-fA-F]{6})<([^>]*)>/.exec(remaining)
    if (colorMatch) {
      segments.push({ type: 'text', text: colorMatch[2], color: '#' + colorMatch[1] })
      remaining = remaining.slice(colorMatch[0].length)
      continue
    }

    // Plain text: consume up to the next token or end of string
    const nextToken = remaining.search(/\[img:|&[0-9a-fA-F]{6}</)
    if (nextToken === -1) {
      segments.push({ type: 'text', text: remaining })
      break
    }
    const cutAt = nextToken === 0 ? 1 : nextToken
    segments.push({ type: 'text', text: remaining.slice(0, cutAt) })
    remaining = remaining.slice(cutAt)
  }

  return segments
}
